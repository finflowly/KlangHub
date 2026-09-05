using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using KlangHub.Streaming;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace KlangHub.Platform.Audio
{
    /// <summary>
    /// WASAPI-based <see cref="IAudioCaptureEngine"/>. Adapted from the original LoopbackRecorder:
    /// the capture / buffer / event-thread logic is preserved verbatim; only the I/O edges change
    /// from direct <c>IMainForm</c> calls to settings-in / events-out, and NAudio types are converted
    /// to the neutral Core.Audio types at this Platform boundary.
    /// </summary>
    public sealed class LoopbackCaptureEngine : IAudioCaptureEngine
    {
        private WasapiRecorder? soundIn;
        private bool isRecording = false;
        private WaveFormat? waveFormat;
        /// <summary>When audio last arrived. UTC throughout, so the silence watchdog measures a
        /// gap and not the offset to local time - and so the hour the clocks change is not a silence.</summary>
        private DateTime latestDataAvailable;
        private System.Timers.Timer? dataAvailableTimer;
        private System.Timers.Timer? getDevicesTimer;
        private readonly ILogger logger;
        private BufferBlock bufferCaptured = null!, bufferSend = null!;
        private readonly object bufferSwapSync = new();
        private AudioCaptureSettings settings = null!;

        /// <summary>
        /// Serialises everything that starts or stops a recorder. Without it two threads build a recorder
        /// at the same time - which is not theoretical: Start() used to enable the 15-second scan timer and
        /// THEN run the first scan inline on the UI thread, and that first scan takes longer than 15 seconds
        /// on this machine (device enumeration, the silence player, and Apply() from AddRecordingDevices).
        /// </summary>
        private readonly object startSync = new();

        /// <summary>The single drain loop and its stop flag. See <see cref="EnsureEventLoop"/>.</summary>
        private sealed class EventLoopState
        {
            public volatile bool Run = true;
            public int Active;
        }

        private EventLoopState? eventLoop;
        private int eventLoopStarted;

        public event EventHandler<AudioFrame>? DataAvailable;
        public event EventHandler<byte[]>? LevelSampled;
        public event EventHandler<AudioDeviceListEventArgs>? DevicesChanged;

        public LoopbackCaptureEngine(ILogger loggerIn)
        {
            logger = loggerIn;
        }

        /// <summary>Begin periodic device scanning and start capturing with the given settings.</summary>
        public void Start(AudioCaptureSettings settingsIn)
        {
            if (settingsIn == null)
                return;

            settings = settingsIn;

            EnsureEventLoop();

            // The first scan runs BEFORE the periodic one is armed. The other way round, a first scan that
            // outlasts the interval is re-entered by the timer thread while the UI thread is still inside it.
            DoStart(null, null);

            getDevicesTimer = new System.Timers.Timer
            {
                Interval = 15000,
                Enabled = true
            };
            getDevicesTimer.Elapsed += new ElapsedEventHandler(DoStart);
            getDevicesTimer.Start();
        }

        /// <summary>
        /// One scan: refresh the endpoint list, and start capture if none is running.
        ///
        /// Never re-entered. A periodic scan that arrives while another one is still working is SKIPPED, not
        /// queued - the next one is only fifteen seconds away, and a queue of scans is how two recorders end
        /// up being built for the same endpoint.
        /// </summary>
        private void DoStart(object? sender, ElapsedEventArgs? e)
        {
            if (!Monitor.TryEnter(startSync))
                return;

            try
            {
                ScanDevices();
                if (!isRecording)
                {
                    StartRecording();
                }
            }
            finally
            {
                Monitor.Exit(startSync);
            }
        }

        private void StartRecording()
        {
            lock (startSync)
            {
                if (isRecording)
                    return;

                StartSilenceCheckTimer();
                StartConfiguredDevice();
            }
        }

        /// <summary>Switch capture to the given settings (device / format / stereo change).</summary>
        public bool Apply(AudioCaptureSettings settingsIn)
        {
            if (settingsIn == null)
                return false;

            lock (startSync)
            {
                settings = settingsIn;
                StopRecording();
                StartSilenceCheckTimer();
                return StartConfiguredDevice();
            }
        }

        /// <summary>
        /// Resolve the endpoint from the current settings and start capture. Tries the configured
        /// device first, then falls back to the first endpoint that starts (was the fallback loop in
        /// MainForm.StartRecordingDevice, now owned by the engine).
        /// </summary>
        private bool StartConfiguredDevice()
        {
            var devices = EnumerateEndpoints();
            if (devices.Count == 0)
            {
                logger.Log(KlangHub.Properties.Strings.MessageBox_NoRecordingDevices);
                return false;
            }

            var preferred = devices.FirstOrDefault(d => d.ID == settings?.DeviceId);
            if (preferred != null && TryStartCapture(preferred))
                return true;

            foreach (var device in devices)
            {
                if (TryStartCapture(device))
                    return true;
            }

            // Nothing captured, from any endpoint. Every individual failure was logged as an exception,
            // but the CONSEQUENCE was not - and the consequence is total silence on every speaker while
            // the tiles still say "playing", because the Cast side is perfectly healthy and simply has
            // nothing to send. Anyone reading a log needs to see that in one line.
            logger.Log($"NO AUDIO: capture could not be started on any of the {devices.Count} endpoints. " +
                       "Nothing will be streamed until this succeeds - see the errors above for why.");
            return false;
        }

        /// <summary>Start WASAPI capture on a specific endpoint. (Was StartRecordingSetDevice.)</summary>
        private bool TryStartCapture(MMDevice recordingDevice)
        {
            if (recordingDevice == null)
                return false;

            try
            {
                // NAudio 3 builds the recorder with its format already decided, so the device's own mix
                // format has to be read first - it used to be read off the half-constructed capture object.
                // Same numbers, one step earlier.
                WaveFormat mixFormat;
                using (var probe = recordingDevice.CreateAudioClient())
                    mixFormat = probe.MixFormat;

                WaveFormat captureFormat = mixFormat;
                var selectedFormat = settings.StreamFormat;
                var convertMultiChannelToStereo = settings.ConvertMultiChannelToStereo;
                var nrChannels = convertMultiChannelToStereo ? mixFormat.Channels : 2;
                // Cap the capture rate at 48 kHz. Cast receivers force a 48 kHz internal mixer, so a 96 kHz
                // stream is resampled away on-device anyway - capping it here halves the on-wire bitrate (e.g.
                // 32-bit stereo 6 -> 3 Mbit/s) with no audible loss, cutting the underrun/"noise" risk on
                // weak-Wi-Fi speakers. (48 kHz is also the max LAME accepts for MP3.) VERIFIED on this hardware:
                // WASAPI shared-mode genuinely converts the 32-bit-float mix to the requested rate/depth.
                var rate = System.Math.Min(mixFormat.SampleRate, 48000);
                switch (selectedFormat)
                {
                    case SupportedStreamFormat.Wav:
                        captureFormat = new WaveFormat(44100, 16, nrChannels);
                        break;
                    case SupportedStreamFormat.Mp3_320:
                    case SupportedStreamFormat.Mp3_128:
                        captureFormat = new WaveFormat(rate, 16, 2);
                        break;
                    case SupportedStreamFormat.Wav_16bit:
                        captureFormat = new WaveFormat(rate, 16, nrChannels);
                        break;
                    case SupportedStreamFormat.Wav_24bit:
                        captureFormat = new WaveFormat(rate, 24, nrChannels);
                        break;
                    case SupportedStreamFormat.Wav_32bit:
                        captureFormat = new WaveFormat(rate, 32, nrChannels);
                        break;
                    case SupportedStreamFormat.Flac:
                        // FLAC needs INTEGER PCM (the WASAPI mix format is typically 32-bit float). 24-bit int =
                        // true HiFi, losslessly FLAC-compressed (verified: FLAKE round-trips 24-bit byte-exact).
                        // FLAC is the out-of-box default: lossless like WAV but COMPRESSED, so it doesn't OOM
                        // small speakers the way 32-bit uncompressed LPCM did on the Enchant (ERROR 102).
                        captureFormat = new WaveFormat(rate, 24, nrChannels);
                        break;
                    default:
                        break;
                }
                // No WithLowLatency here, and it is not an oversight. IAudioClient3 low-latency capture
                // requires no loopback and a capture format identical to the device mix format - and this
                // app is loopback by definition (it streams what the PC plays) and asks for a format of its
                // own (48 kHz 24-bit from a 96 kHz float mix). Neither condition can be met, so the
                // low-latency path is unreachable here in principle. Asking for it as REQUIRED threw on
                // every start and left the app with no capture at all: silence on every speaker.
                var builder = new WasapiRecorderBuilder()
                    .WithDevice(recordingDevice)
                    .WithFormat(captureFormat)
                    // This one does apply, and is the real gain: the capture thread is the one thread here
                    // that must not be preempted, because a packet missed at this end is a hole in the
                    // stream for every speaker at once.
                    .WithMmcssThreadPriority("Pro Audio");

                if (recordingDevice.DataFlow == DataFlow.Render)
                    builder = builder.WithLoopbackCapture();

                soundIn = builder.Build();

                waveFormat = soundIn.WaveFormat;
                logger.Log($"Stream format set to {waveFormat.Encoding} {waveFormat.SampleRate} {waveFormat.BitsPerSample} bit");
                soundIn.DataAvailable += OnDataAvailable;
                soundIn.RecordingStopped += OnRecordingStopped;
                soundIn.StartRecording();
                isRecording = true;

                // Worth a line even though the mode is now fixed: it is the first number in the chain, and
                // the one a multi-room delay is measured from.
                logger.Log($"Capture latency {soundIn.LatencyMilliseconds:F1} ms, MMCSS \"Pro Audio\"");

                var bytesPerSecond = soundIn.WaveFormat.SampleRate * soundIn.WaveFormat.Channels * (soundIn.WaveFormat.BitsPerSample / 8);
                lock (bufferSwapSync)
                {
                    bufferCaptured = new BufferBlock() { Data = new byte[bytesPerSecond / 2] };
                    bufferSend = new BufferBlock() { Data = new byte[bytesPerSecond / 2] };
                }

                // NOT started here. The drain loop belongs to the engine, not to one recorder - starting it
                // per capture is what put two of them on the same encoder. See EnsureEventLoop.
                EnsureEventLoop();

                return true;
            }
            catch (Exception ex)
            {
                logger.Log(ex, "Error initializing the recording device:");
            }

            return false;
        }

        /// <summary>
        /// A packet straight from WASAPI, zero-copy.
        ///
        /// The buffer is a view onto WASAPI's own memory and is only valid inside this call, so it is
        /// copied into the ring here - which is what the old handler did anyway, just from a byte[] NAudio
        /// had already allocated for us. One allocation per packet less, about fifty times a second.
        ///
        /// <paramref name="qpcPosition"/> is the moment the device captured this packet, on the same
        /// QueryPerformanceCounter clock the whole machine shares. It is not used yet; it is the anchor the
        /// planned multi-room synchronisation needs, and it only exists because the capture reports it -
        /// there is no way to recover it later. See docs/PLAN-MULTIROOM-SYNC.md.
        /// </summary>
        private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags,
                                     long devicePosition, long qpcPosition)
        {
            if (soundIn == null || soundIn.WaveFormat == null)
                return;

            latestDataAvailable = DateTime.UtcNow;
            lastCaptureQpc = qpcPosition;

            lock (bufferSwapSync)
                bufferCaptured.Add(buffer);
        }

        /// <summary>When the last packet was captured, on the machine's QueryPerformanceCounter clock.</summary>
        private long lastCaptureQpc;

        /// <summary>Enumerate the currently available capture/render endpoints (neutral).</summary>
        public IReadOnlyList<AudioCaptureDevice> GetDevices()
        {
            return EnumerateEndpoints().Select(ToDevice).ToList();
        }

        /// <summary>The current system default render endpoint, or null.</summary>
        public AudioCaptureDevice GetDefaultDevice()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                return ToDevice(enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia));
            }
            catch (Exception ex)
            {
                logger.Log(ex, "LoopbackCaptureEngine.GetDefaultDevice");
                return null!;
            }
        }

        /// <summary>Enumerate endpoints and raise <see cref="DevicesChanged"/> (was IMainForm.AddRecordingDevices).</summary>
        private void ScanDevices()
        {
            var endpoints = EnumerateEndpoints();
            if (endpoints.Count == 0)
                return;

            var devices = endpoints.Select(ToDevice).ToList();
            AudioCaptureDevice defaultDevice = GetDefaultDevice();
            DevicesChanged?.Invoke(this, new AudioDeviceListEventArgs(devices, defaultDevice));
        }

        private static List<MMDevice> EnumerateEndpoints()
        {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active).ToList();
        }

        private static AudioCaptureDevice ToDevice(MMDevice device)
        {
            if (device == null)
                return null!;

            // One client, disposed. The old MMDevice.AudioClient property created a fresh COM object on
            // every read - and this read it twice per device, on a list refreshed every fifteen seconds.
            using var client = device.CreateAudioClient();
            var mix = client.MixFormat;
            return new AudioCaptureDevice(
                device.ID,
                device.FriendlyName,
                ToAudioFlow(device.DataFlow),
                mix.SampleRate,
                mix.Channels);
        }

        private static AudioFlow ToAudioFlow(DataFlow flow) => flow switch
        {
            DataFlow.Render => AudioFlow.Render,
            DataFlow.Capture => AudioFlow.Capture,
            _ => AudioFlow.All
        };

        /// <summary>How many drain loops are running. Must never exceed one; see the tests.</summary>
        internal int ActiveEventLoops
        {
            get
            {
                var state = eventLoop;
                return state == null ? 0 : Volatile.Read(ref state.Active);
            }
        }

        /// <summary>
        /// Start the one drain loop this engine ever has, if it is not already running.
        ///
        /// It used to be created inside TryStartCapture, so every restart added another and the old one kept
        /// going: it only exited when isRecording was false, and a restart set that back to true within
        /// milliseconds. Two loops then pushed frames into the SAME stateful encoder from two threads. FLAKE
        /// writes its blocks through unsafe pointers, so that is not a garbled stream but writes past the end
        /// of managed arrays - the 2026-09-05 dump has two of these stacks inside FlacEncoder.Encode and a
        /// heap with 21 corrupted objects, which is the access violation observed seconds after the sound
        /// broke off.
        ///
        /// Idempotent and safe from any thread.
        /// </summary>
        internal void EnsureEventLoop()
        {
            if (Interlocked.CompareExchange(ref eventLoopStarted, 1, 0) != 0)
                return;

            var state = new EventLoopState();
            eventLoop = state;

            new Thread(EventLoopBody)
            {
                Name = "Loopback Event Thread",
                IsBackground = true
            }.Start(Tuple.Create(new WeakReference<LoopbackCaptureEngine>(this), state));
        }

        /// <summary>Ends the drain loop. The engine cannot be restarted afterwards; only Dispose calls it.</summary>
        private void StopEventLoop()
        {
            var state = eventLoop;
            if (state != null)
                state.Run = false;
        }

        private static void EventLoopBody(object? param)
        {
            var carrier = (Tuple<WeakReference<LoopbackCaptureEngine>, EventLoopState>)param!;
            var thisRef = carrier.Item1;
            var state = carrier.Item2;
            Interlocked.Increment(ref state.Active);
            try
            {
                while (state.Run)
                {
                    if (!thisRef.TryGetTarget(out LoopbackCaptureEngine? engine) || engine == null)
                    {
                        // Instance is dead
                        return;
                    }

                    // The loop now outlives an individual recorder, so idle between captures is normal and
                    // must not end it - a restart would otherwise have nothing draining its buffers.
                    if (!engine.isRecording || engine.bufferSend == null)
                    {
                        engine = null;
                        Thread.Sleep(10);
                        continue;
                    }

                    engine.SwapBuffer();
                    if (engine.bufferSend.Used > 0)
                    {
                        // Tier2-A3: still a fresh array (downstream ApplicationBuffer retains the reference,
                        // so it must not be pooled/reused) — but AsSpan().ToArray() drops the LINQ per-byte
                        // enumerator on this up-to-1000 Hz path.
                        var bytes = engine.bufferSend.Data.AsSpan(0, engine.bufferSend.Used).ToArray();
                        engine.RaiseDataAvailable(bytes);
                        engine.bufferSend.Used = 0;
                        engine.LevelSampled?.Invoke(engine, bytes);
                    }

                    // Drop the strong reference before sleeping, so a disposed engine can still be collected.
                    engine = null;
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());

                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
            finally
            {
                Interlocked.Decrement(ref state.Active);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (isRecording)
            {
                logger.Log("Recording Stopped");

                // Through the same door as everybody else. NAudio raises this event inline on its own
                // capture thread when no SynchronizationContext was captured, and disposing a recorder
                // from its own thread would make it join itself - forever.
                StopRecording();
            }
        }

        private void StartSilenceCheckTimer()
        {
            if (dataAvailableTimer == null)
            {
                latestDataAvailable = DateTime.UtcNow;
                dataAvailableTimer = new System.Timers.Timer
                {
                    Interval = 1000,
                    Enabled = true
                };
                dataAvailableTimer.Elapsed += new ElapsedEventHandler(OnCheckForSilence);
                dataAvailableTimer.Start();
            }
        }

        /// <summary>Long enough with no audio that the devices need something sent, or they give up.</summary>
        private static readonly TimeSpan SilenceBeforeKeepAlive = TimeSpan.FromSeconds(5);

        /// <summary>Shorter than that, but long enough to be worth a line when the sound later stops.</summary>
        private static readonly TimeSpan SilenceWorthNoting = TimeSpan.FromSeconds(2);

        private void OnCheckForSilence(object? sender, ElapsedEventArgs e)
        {
            if (waveFormat == null)
                return;

            // The gap is measured once, before anything is reset. It used to be read after
            // latestDataAvailable had already been set to now, so the line meant to say how long the
            // sound had been missing always said about zero - and the second test, which was supposed to
            // report a shorter gap, could never be true again. The one number worth having when the music
            // stops was written down and thrown away in the same breath.
            var quietFor = DateTime.UtcNow - latestDataAvailable;

            if (quietFor > SilenceBeforeKeepAlive)
            {
                latestDataAvailable = DateTime.UtcNow;
                var silence = new WavGenerator().GetSilenceBytes(1);
                RaiseDataAvailable(silence);
                logger.Log($"Check For Silence: nothing captured for {quietFor.TotalSeconds:F1}s - sending a second of silence.");
            }
            else if (quietFor > SilenceWorthNoting)
            {
                logger.Log($"Check For Silence: nothing captured for {quietFor.TotalSeconds:F1}s.");
            }
        }

        private void RaiseDataAvailable(byte[] bytes)
        {
            var wf = waveFormat;
            if (wf == null || bytes == null)
                return;

            DataAvailable?.Invoke(this, new AudioFrame(bytes, wf.SampleRate, wf.BitsPerSample, wf.Channels));
        }

        private void SwapBuffer()
        {
            lock (bufferSwapSync)
            {
                var tmp = bufferCaptured;
                bufferCaptured = bufferSend;
                bufferSend = tmp;
            }
        }

        public void Stop()
        {
            StopRecording();
        }

        /// <summary>
        /// Ends capture without ever blocking the caller.
        ///
        /// NAudio 3's WasapiRecorder.StopRecording() only writes a flag, and its Dispose() joins the
        /// capture thread with NO timeout. Between StartRecording() and the moment the capture thread
        /// writes "captureState = Capturing" itself, that flag is overwritten - the stop is lost, the
        /// capture loop never ends, and the join never returns.
        ///
        /// KlangHub walks straight into that window: the engine starts capture while it is being built,
        /// and MainForm_Load applies the saved settings milliseconds later. Measured on this machine,
        /// three starts out of six froze in exactly that spot, with the UI thread parked in Thread.Join
        /// inside Form.OnLoad - no device list, no reaction to the close button, nothing left but the
        /// Task Manager. That is the "App ist gecrasht beim Start" reported from a real desktop.
        ///
        /// Two defences, because neither alone is enough:
        ///   the stop is re-issued until the recorder reports itself stopped, which defeats the lost
        ///     flag no matter where in its start-up the recorder happened to be;
        ///   and all of it, the join included, happens on a thread of our own. NAudio marks its capture
        ///     thread IsBackground, so even one that never returns cannot keep the process alive.
        ///
        /// The caller is free to build the next recorder immediately: the old one's handlers are
        /// detached here, and WASAPI shared-mode loopback has no objection to a brief overlap.
        /// </summary>
        private void StopRecording()
        {
            isRecording = false;

            var recorder = soundIn;
            soundIn = null;
            if (recorder == null)
                return;

            recorder.DataAvailable -= OnDataAvailable;
            recorder.RecordingStopped -= OnRecordingStopped;

            new Thread(() => ShutDownRecorder(recorder))
            {
                Name = "Loopback Capture Shutdown",
                IsBackground = true,
            }.Start();
        }

        /// <summary>Stops and releases one recorder, off the caller's thread. See StopRecording.</summary>
        private void ShutDownRecorder(WasapiRecorder recorder)
        {
            try
            {
                // Every 20 ms for at most three seconds. The capture loop looks at the flag at least
                // every 300 ms, so a stop that is lost to the start-up race is simply re-sent until it
                // is seen - and once the recorder is stopped, the join below returns at once.
                for (int i = 0; i < 150 && recorder.CaptureState != CaptureState.Stopped; i++)
                {
                    recorder.StopRecording();
                    Thread.Sleep(20);
                }

                if (recorder.CaptureState != CaptureState.Stopped)
                    logger.Log("Capture did not stop within three seconds; releasing it anyway.");

                recorder.Dispose();
            }
            catch (Exception ex)
            {
                logger.Log(ex, "LoopbackCaptureEngine.ShutDownRecorder");
            }
        }

        public void Restart()
        {
            lock (startSync)
            {
                StopRecording();
                ScanDevices();
                if (!isRecording)
                    StartRecording();
            }
        }

        public void Dispose()
        {
            StopRecording();
            StopEventLoop();
            dataAvailableTimer?.Close();
            dataAvailableTimer?.Dispose();
            getDevicesTimer?.Close();
            getDevicesTimer?.Dispose();
        }
    }
}
