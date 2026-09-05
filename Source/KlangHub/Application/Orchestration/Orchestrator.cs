using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KlangHub.Classes;
using KlangHub.Platform.Audio;
using KlangHub.Application.Interfaces;
using KlangHub.Streaming.Interfaces;
using KlangHub.Rest;

namespace KlangHub.Application.Orchestration
{
    /// <summary>
    /// 2.2b-H4b: WinForms-free orchestration extracted from the ApplicationLogic god-object. Owns the
    /// capture -> encode -> fan-out streaming pipeline, the streaming-listener lifecycle, discovery start,
    /// the task runner and the ICastHost host surface. ApplicationLogic is now a thin tray shell that
    /// delegates here. It lives in the app project for now (IDevices transitively references the Platform
    /// IDevice, so it cannot yet move to Core), but depends only on neutral contracts, so it is testable
    /// without WinForms.
    /// </summary>
    public sealed class Orchestrator
    {
        private readonly IDevices devices;
        private readonly IStreamingRequestsListener streamingRequestListener;
        private readonly ICastProvider castProvider;
        private readonly IDeviceStatusTimer deviceStatusTimer;
        private readonly ILogger logger;
        private readonly TasksToCancel taskList = new TasksToCancel();

        /// <summary>Raised when a device is added/removed; the tray shell reacts with WinForms (menu items).
        /// Raised on the discovery / status-timer thread - subscribers must marshal their UI work.</summary>
        public event Action<IDevice>? DeviceAdded;
        public event Action<IDevice>? DeviceRemoved;

        // 2.2b-M2: Chromecast audio delivery behind the neutral IAudioSink seam. OnRecordingDataAvailable
        // fans raw PCM to every sink; today there is one (Chromecast). AirPlay/Snapcast sinks join here later.
        private readonly ChromecastAudioSink chromecastSink;
        private readonly IAudioSink[] audioSinks;
        private string streamTitle = Properties.Strings.ChromeCast_StreamTitle;
        private bool autoRestart = false;

        public Orchestrator(IDevices devicesIn, IStreamingRequestsListener streamingRequestListenerIn,
            ICastProvider castProviderIn, IDeviceStatusTimer deviceStatusTimerIn, ILogger loggerIn)
        {
            devices = devicesIn;
            streamingRequestListener = streamingRequestListenerIn;
            castProvider = castProviderIn;
            deviceStatusTimer = deviceStatusTimerIn;
            logger = loggerIn;
            chromecastSink = new ChromecastAudioSink(devicesIn, loggerIn);
            audioSinks = new IAudioSink[] { chromecastSink };
            devices.SetCallback(RaiseDeviceAdded);
            devices.SetRemoveCallback(RaiseDeviceRemoved);
        }

        // ---------- device add/remove eventing + neutral session bridging ----------

        private void RaiseDeviceAdded(IDevice device) => DeviceAdded?.Invoke(device);

        private void RaiseDeviceRemoved(IDevice device) => DeviceRemoved?.Invoke(device);

        /// <summary>Toggle Play/Stop for one device via the neutral casting session. Swallows the
        /// throw-on-miss (device left the registry) to a no-op, matching the old direct path.</summary>
        public void TogglePlayStop(CastDeviceDescriptor descriptor)
        {
            try
            {
                castProvider.CreateSession(descriptor).TogglePlayStop();
            }
            catch (InvalidOperationException ex)
            {
                logger.Log(ex, "Orchestrator.TogglePlayStop");
            }
        }

        /// <summary>Single-device status refresh via the neutral session (RequestStatus maps 1:1 to
        /// OnGetStatus). Called at add time when the device is present, so no miss-guard is needed.</summary>
        public void RequestDeviceStatus(IDevice deviceIn)
        {
            if (castProvider != null && deviceIn is IPlaybackSession playbackSession)
                castProvider.CreateSession(playbackSession.Device).RequestStatus();
            else
                deviceIn.OnGetStatus();
        }

        /// <summary>name/device -> neutral session resolver for the REST handler. Wraps CreateSession's
        /// throw-on-miss into a null return so REST sites simply skip a vanished device.</summary>
        public IPlaybackSession ResolveSession(IDevice device)
        {
            if (castProvider == null || !(device is IPlaybackSession playbackSession))
                return null!;
            try { return castProvider.CreateSession(playbackSession.Device); }
            catch (InvalidOperationException) { return null!; }
        }

        public void StartStatusPolling()
        {
            deviceStatusTimer.StartPollingDevice(devices.OnGetStatus);
        }

        public void StartRestApi(IPAddress ipAddress, Action restartRecording)
        {
            StartTask(() =>
            {
                new RestApi().StartListening(ipAddress,
                    (socket, req, devs, log, restart) => RestApiHandler.Process(socket, req, devs, log, restart, ResolveSession),
                    logger, devices, restartRecording);
            });
        }

        /// <summary>Neutral startup sequence: discovery + status polling + streaming listener + REST.
        /// The shell does the UI-side AddNotifyIcon/LoadSettings/config load first, then calls this.</summary>
        public void Start(Action restartRecording)
        {
            ScanForDevices();
            StartStatusPolling();
            var ipAddress = Network.GetIp4Address();
            if (ipAddress == null)
            {
                logger.Log(Properties.Strings.MessageBox_NoIPAddress);
                return;
            }
            StartStreamingListener(ipAddress);
            StartRestApi(ipAddress, restartRecording);
        }

        // ---------- streaming pipeline ----------

        public void OnStreamingRequestConnect(Socket socketIn, string httpRequestIn)
            => chromecastSink.AcceptStreamingConnection(socketIn, httpRequestIn);

        /// <summary>Fan one captured (raw PCM) frame out to every active audio sink (2.2b-M2).</summary>
        public void OnRecordingDataAvailable(AudioFrame frame)
        {
            foreach (var sink in audioSinks)
                sink.Write(frame);
        }

        public void ClearMp3Buffer() => chromecastSink.ClearEncoder();

        public void SetStreamFormat(SupportedStreamFormat formatIn) => chromecastSink.SetStreamFormat(formatIn);

        public void SetLagThreshold(int lagThresholdIn) => chromecastSink.SetLagThreshold(lagThresholdIn);

        /// <summary>Read the current stream format (the settings shell persists it).</summary>
        public SupportedStreamFormat GetStreamFormat() => chromecastSink.StreamFormat;

        // ---------- discovery start + streaming-listener lifecycle ----------

        public void ScanForDevices()
        {
            if (devices == null || castProvider == null)
                return;

            // The user pressing "Scan again" means "look properly this time" - restarting mDNS while the
            // throttle still recognises every device would make the button do nothing visible.
            devices.ForgetDiscoveryThrottle();
            castProvider.Discovery.Start();
        }

        public void StartStreamingListener(IPAddress ipAddress)
        {
            StartTask(() =>
            {
                streamingRequestListener.StartListening(ipAddress, OnStreamingRequestConnect, logger);
            });
        }

        public void ChangeIPAddressUsed(IPAddress ipAddressIn)
        {
            if (devices == null || streamingRequestListener == null)
                return;

            logger.Log($"Change IP4 address: {ipAddressIn}");
            devices.Stop();
            streamingRequestListener.StopListening();
            ScanForDevices();
            StartTask(() =>
            {
                streamingRequestListener.StartListening(ipAddressIn, OnStreamingRequestConnect, logger);
            });
            var cancellationTokenSource = new CancellationTokenSource();
            StartTask(() =>
            {
                Task.Delay(2500).Wait();

                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                devices.Start();
            }, cancellationTokenSource);
        }

        // ---------- ICastHost surface (delegated to by ApplicationLogic, which implements ICastHost) ----------

        public string GetStreamingUrl()
        {
            if (streamingRequestListener == null)
                return null!;

            return streamingRequestListener.GetStreamimgUrl();
        }

        public string GetStreamTitle() => streamTitle;

        /// <summary>The artwork URL on our own streaming server (same host/port as the audio stream), which the
        /// receiver fetches for the full-bleed now-playing image.</summary>
        public string GetArtworkUrl()
        {
            var baseUrl = GetStreamingUrl();
            if (string.IsNullOrEmpty(baseUrl))
                return string.Empty;

            // The language is part of the URL on purpose: a receiver caches the image per URL, so a plain
            // /artwork.png would keep showing the language of the first cast for the rest of the session.
            var lang = Classes.ArtworkRenderer.Culture.TwoLetterISOLanguageName;
            var path = "artwork.png?lang=" + lang;

            // The cover's fingerprint rides along for the same reason the language does: receivers cache by
            // URL, so without an address that changes with the picture, the first cover of the evening would
            // stay on screen for the rest of the album.
            var cover = Platform.NowPlaying.CurrentCover.Fingerprint;
            if (!string.IsNullOrEmpty(cover))
                path += "&cover=" + cover;
            return baseUrl.EndsWith("/") ? baseUrl + path : baseUrl + "/" + path;
        }

        public void SetStreamTitle(string title) => streamTitle = title;

        public bool GetAutoRestart() => autoRestart;

        public void SetAutoRestart(bool autoRestartIn) => autoRestart = autoRestartIn;

        public void StartTask(Action action, CancellationTokenSource? cancellationTokenSource = null)
        {
            taskList.Add(action, cancellationTokenSource);
        }

        // ---------- teardown (the shell still owns devices / notifyIcon / mainForm / hook teardown) ----------

        /// <summary>Stops+disposes the streaming listener and the mp3 encoder (kept in the shell's dispose order).</summary>
        public void StopAndDisposeStreaming()
        {
            streamingRequestListener?.StopListening();
            streamingRequestListener?.Dispose();
            chromecastSink.DisposeEncoder();
        }

        public void DisposeTaskList()
        {
            taskList?.Dispose();
        }
    }
}
