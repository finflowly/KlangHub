using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using KlangHub.Application.Interfaces;
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
        private WasapiCapture soundIn;
        private bool isRecording = false;
        private WaveFormat waveFormat;
        private DateTime latestDataAvailable;
        private System.Timers.Timer dataAvailableTimer;
        private System.Timers.Timer getDevicesTimer;
        private readonly ILogger logger;
        private Thread eventThread;
        private BufferBlock bufferCaptured, bufferSend;
        private readonly object bufferSwapSync = new();
        private AudioCaptureSettings settings;

        public event EventHandler<AudioFrame> DataAvailable;
        public event EventHandler<byte[]> LevelSampled;
        public event EventHandler<AudioDeviceListEventArgs> DevicesChanged;

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

            getDevicesTimer = new System.Timers.Timer
            {
                Interval = 15000,
                Enabled = true
            };
            getDevicesTimer.Elapsed += new ElapsedEventHandler(DoStart);
            getDevicesTimer.Start();

            DoStart(null, null);
        }

        private void DoStart(object sender, ElapsedEventArgs e)
        {
            ScanDevices();
            if (!isRecording)
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            if (isRecording)
                return;

            StartSilenceCheckTimer();
            StartConfiguredDevice();
        }

        /// <summary>Switch capture to the given settings (device / format / stereo change).</summary>
        public bool Apply(AudioCaptureSettings settingsIn)
        {
            if (settingsIn == null)
                return false;

            settings = settingsIn;
            StopRecording();
            StartSilenceCheckTimer();
            return StartConfiguredDevice();
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

            return false;
        }

        /// <summary>Start WASAPI capture on a specific endpoint. (Was StartRecordingSetDevice.)</summary>
        private bool TryStartCapture(MMDevice recordingDevice)
        {
            if (recordingDevice == null)
                return false;

            try
            {
                if (recordingDevice.DataFlow == DataFlow.Render)
                {
                    soundIn = new WasapiLoopbackCapture(recordingDevice);
                }
                else
                {
                    soundIn = new WasapiCapture(recordingDevice);
                }

                var selectedFormat = settings.StreamFormat;
                var convertMultiChannelToStereo = settings.ConvertMultiChannelToStereo;
                var nrChannels = convertMultiChannelToStereo ? soundIn.WaveFormat.Channels : 2;
                switch (selectedFormat)
                {
                    case SupportedStreamFormat.Wav:
                        soundIn.WaveFormat = new WaveFormat(44100, 16, nrChannels);
                        break;
                    case SupportedStreamFormat.Mp3_320:
                    case SupportedStreamFormat.Mp3_128:
                        soundIn.WaveFormat = new WaveFormat(soundIn.WaveFormat.SampleRate, 16, 2);
                        break;
                    case SupportedStreamFormat.Wav_16bit:
                        soundIn.WaveFormat = new WaveFormat(soundIn.WaveFormat.SampleRate, 16, nrChannels);
                        break;
                    case SupportedStreamFormat.Wav_24bit:
                        soundIn.WaveFormat = new WaveFormat(soundIn.WaveFormat.SampleRate, 24, nrChannels);
                        break;
                    case SupportedStreamFormat.Wav_32bit:
                        soundIn.WaveFormat = new WaveFormat(soundIn.WaveFormat.SampleRate, 32, nrChannels);
                        break;
                    default:
                        break;
                }
                waveFormat = soundIn.WaveFormat;
                logger.Log($"Stream format set to {waveFormat.Encoding} {waveFormat.SampleRate} {waveFormat.BitsPerSample} bit");
                soundIn.DataAvailable += OnDataAvailable;
                soundIn.RecordingStopped += OnRecordingStopped;
                soundIn.StartRecording();
                isRecording = true;

                var bytesPerSecond = soundIn.WaveFormat.SampleRate * soundIn.WaveFormat.Channels * (soundIn.WaveFormat.BitsPerSample / 8);
                bufferCaptured = new BufferBlock() { Data = new byte[bytesPerSecond / 2] };
                bufferSend = new BufferBlock() { Data = new byte[bytesPerSecond / 2] };

                eventThread = new Thread(EventThread)
                {
                    Name = "Loopback Event Thread",
                    IsBackground = true
                };
                eventThread.Start(new WeakReference<LoopbackCaptureEngine>(this));

                return true;
            }
            catch (Exception ex)
            {
                logger.Log(ex, "Error initializing the recording device:");
            }

            return false;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (soundIn == null || soundIn.WaveFormat == null)
                return;

            latestDataAvailable = DateTime.Now;

            lock (bufferSwapSync)
            {
                var currentBuffer = bufferCaptured;
                currentBuffer.Add(e.Buffer, e.BytesRecorded);
            }
        }

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
                return null;
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
                return null;

            return new AudioCaptureDevice(
                device.ID,
                device.FriendlyName,
                ToAudioFlow(device.DataFlow),
                device.AudioClient.MixFormat.SampleRate,
                device.AudioClient.MixFormat.Channels);
        }

        private static AudioFlow ToAudioFlow(DataFlow flow) => flow switch
        {
            DataFlow.Render => AudioFlow.Render,
            DataFlow.Capture => AudioFlow.Capture,
            _ => AudioFlow.All
        };

        private static void EventThread(object param)
        {
            var thisRef = (WeakReference<LoopbackCaptureEngine>)param;
            try
            {
                while (true)
                {
                    if (!thisRef.TryGetTarget(out LoopbackCaptureEngine engine) || engine == null)
                    {
                        // Instance is dead
                        return;
                    }

                    if (!engine.isRecording)
                    {
                        return;
                    }

                    engine.SwapBuffer();
                    if (engine.bufferSend.Used > 0)
                    {
                        var bytes = engine.bufferSend.Data.Take(engine.bufferSend.Used).ToArray();
                        engine.RaiseDataAvailable(bytes);
                        engine.bufferSend.Used = 0;
                        engine.LevelSampled?.Invoke(engine, bytes);
                    }

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
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (isRecording)
            {
                logger.Log("Recording Stopped");
                isRecording = false;
                if (soundIn != null)
                {
                    soundIn.DataAvailable -= OnDataAvailable;
                    soundIn.RecordingStopped -= OnRecordingStopped;
                }
                soundIn?.StopRecording();
                soundIn?.Dispose();
                soundIn = null;
            }
        }

        private void StartSilenceCheckTimer()
        {
            if (dataAvailableTimer == null)
            {
                latestDataAvailable = DateTime.Now;
                dataAvailableTimer = new System.Timers.Timer
                {
                    Interval = 1000,
                    Enabled = true
                };
                dataAvailableTimer.Elapsed += new ElapsedEventHandler(OnCheckForSilence);
                dataAvailableTimer.Start();
            }
        }

        private void OnCheckForSilence(object sender, ElapsedEventArgs e)
        {
            if (waveFormat == null)
                return;

            if ((DateTime.Now - latestDataAvailable).TotalSeconds > 5)
            {
                latestDataAvailable = DateTime.Now;
                var silence = new WavGenerator().GetSilenceBytes(1);
                RaiseDataAvailable(silence);
                logger.Log($"Check For Silence: Send Silence ({(DateTime.Now - latestDataAvailable).TotalSeconds})");
            }
            if ((DateTime.Now - latestDataAvailable).TotalSeconds > 2)
            {
                logger.Log($"Check For Silence: {(DateTime.Now - latestDataAvailable).TotalSeconds}");
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

        private void StopRecording()
        {
            isRecording = false;

            if (soundIn == null)
                return;

            soundIn?.StopRecording();
            soundIn?.Dispose();
            soundIn = null;
        }

        public void Restart()
        {
            StopRecording();
            DoStart(null, null);
        }

        public void Dispose()
        {
            soundIn?.StopRecording();
            soundIn?.Dispose();
            soundIn = null;
            dataAvailableTimer?.Close();
            dataAvailableTimer?.Dispose();
            getDevicesTimer?.Close();
            getDevicesTimer?.Dispose();
        }
    }
}
