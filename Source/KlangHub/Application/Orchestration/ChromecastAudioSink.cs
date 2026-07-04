using System.Linq;
using System.Net.Sockets;
using KlangHub.Application.Interfaces;
using KlangHub.Platform.Audio;

namespace KlangHub.Application.Orchestration
{
    /// <summary>
    /// 2.2b-M2: the Chromecast audio delivery, extracted from the Orchestrator behind <see cref="IAudioSink"/>.
    /// Owns the MP3/WAV encoding + stream-format + lag state and pushes to the Chromecast device registry
    /// (<see cref="IDevices"/>), which serves the stream over HTTP (DeliveryModel.PullHttp). The encode logic
    /// is moved verbatim from the Orchestrator, so Chromecast output stays byte-identical.
    /// </summary>
    public sealed class ChromecastAudioSink : IAudioSink
    {
        private readonly IDevices devices;
        private readonly ILogger logger;

        private const int trbLagMaximumValue = 1000;
        private int reduceLagThreshold = trbLagMaximumValue;
        private IAudioEncoder mp3Encoder = null;
        private SupportedStreamFormat streamFormatSelected = SupportedStreamFormat.Mp3_320;

        public ChromecastAudioSink(IDevices devicesIn, ILogger loggerIn)
        {
            devices = devicesIn;
            logger = loggerIn;
        }

        /// <summary>The selected stream format (the settings shell persists it).</summary>
        public SupportedStreamFormat StreamFormat => streamFormatSelected;

        public void Write(AudioFrame frame)
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
                // Tier2-A1: dataToSendIn is already frame.Data (a fresh per-frame array read synchronously
                // by the encoder) — the old .ToArray() clone was pure waste.
                mp3Encoder.Encode(dataToSendIn);
                dataToSendIn = mp3Encoder.Read();
            }
            if (dataToSendIn.Length > 0)
            {
                devices.OnRecordingDataAvailable(dataToSendIn, formatIn, reduceLagThreshold, streamFormatSelected);
            }
        }

        /// <summary>A device opened its HTTP streaming connection (Chromecast pulls the stream).</summary>
        public void AcceptStreamingConnection(Socket socketIn, string httpRequestIn)
        {
            if (devices == null)
                return;

            logger.Log(string.Format("Connection added from {0}", socketIn.RemoteEndPoint));
            devices.AddStreamingConnection(socketIn, httpRequestIn, streamFormatSelected);
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

        public void ClearEncoder() => mp3Encoder = null;

        public void SetLagThreshold(int lagThresholdIn) => reduceLagThreshold = lagThresholdIn;

        public void DisposeEncoder() => mp3Encoder?.Dispose();
    }
}
