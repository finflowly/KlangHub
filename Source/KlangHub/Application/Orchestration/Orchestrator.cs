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
        private readonly ILogger logger;
        private readonly TasksToCancel taskList = new TasksToCancel();

        private const int trbLagMaximumValue = 1000;
        private int reduceLagThreshold = trbLagMaximumValue;
        private IAudioEncoder mp3Encoder = null;
        private SupportedStreamFormat streamFormatSelected = SupportedStreamFormat.Mp3_320;
        private string streamTitle = Properties.Strings.ChromeCast_StreamTitle;
        private bool autoRestart = false;

        public Orchestrator(IDevices devicesIn, IStreamingRequestsListener streamingRequestListenerIn,
            ICastProvider castProviderIn, ILogger loggerIn)
        {
            devices = devicesIn;
            streamingRequestListener = streamingRequestListenerIn;
            castProvider = castProviderIn;
            logger = loggerIn;
        }

        // ---------- streaming pipeline ----------

        public void OnStreamingRequestConnect(Socket socketIn, string httpRequestIn)
        {
            if (devices == null)
                return;

            logger.Log(string.Format("Connection added from {0}", socketIn.RemoteEndPoint));
            devices.AddStreamingConnection(socketIn, httpRequestIn, streamFormatSelected);
        }

        public void OnRecordingDataAvailable(AudioFrame frame)
        {
            if (devices == null || frame == null)
                return;

            var formatIn = new AudioFormat(frame.SampleRate, frame.BitsPerSample, frame.Channels);
            var dataToSendIn = frame.Data;

            if (!streamFormatSelected.Equals(SupportedStreamFormat.Wav) &&
                !streamFormatSelected.Equals(SupportedStreamFormat.Wav_16bit) &&
                !streamFormatSelected.Equals(SupportedStreamFormat.Wav_24bit) &&
                !streamFormatSelected.Equals(SupportedStreamFormat.Wav_32bit))
            {
                if (mp3Encoder == null)
                {
                    mp3Encoder = new Mp3Encoder(formatIn, streamFormatSelected, logger);
                }
                mp3Encoder.Encode(dataToSendIn.ToArray());
                dataToSendIn = mp3Encoder.Read();
            }
            if (dataToSendIn.Length > 0)
            {
                devices.OnRecordingDataAvailable(dataToSendIn, formatIn, reduceLagThreshold, streamFormatSelected);
            }
        }

        public void ClearMp3Buffer()
        {
            mp3Encoder = null;
        }

        public void SetStreamFormat(SupportedStreamFormat formatIn)
        {
            if (devices == null)
                return;

            if (formatIn != streamFormatSelected)
            {
                logger.Log($"Set stream format to {formatIn}");
                streamFormatSelected = formatIn;
                mp3Encoder = null;

                devices.Stop();
                devices.Start();
            }
        }

        public void SetLagThreshold(int lagThresholdIn)
        {
            reduceLagThreshold = lagThresholdIn;
        }

        /// <summary>Read the current stream format (the settings shell persists it).</summary>
        public SupportedStreamFormat GetStreamFormat() => streamFormatSelected;

        // ---------- discovery start + streaming-listener lifecycle ----------

        public void ScanForDevices()
        {
            if (devices == null || castProvider == null)
                return;

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
                return null;

            return streamingRequestListener.GetStreamimgUrl();
        }

        public string GetStreamTitle() => streamTitle;

        public void SetStreamTitle(string title) => streamTitle = title;

        public bool GetAutoRestart() => autoRestart;

        public void SetAutoRestart(bool autoRestartIn) => autoRestart = autoRestartIn;

        public void StartTask(Action action, CancellationTokenSource cancellationTokenSource = null)
        {
            taskList.Add(action, cancellationTokenSource);
        }

        // ---------- teardown (the shell still owns devices / notifyIcon / mainForm / hook teardown) ----------

        /// <summary>Stops+disposes the streaming listener and the mp3 encoder (kept in the shell's dispose order).</summary>
        public void StopAndDisposeStreaming()
        {
            streamingRequestListener?.StopListening();
            streamingRequestListener?.Dispose();
            mp3Encoder?.Dispose();
        }

        public void DisposeTaskList()
        {
            taskList?.Dispose();
        }
    }
}
