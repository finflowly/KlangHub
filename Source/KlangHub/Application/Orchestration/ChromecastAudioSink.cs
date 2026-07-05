using System;
using System.Linq;
using System.Net.Sockets;
using KlangHub.Application.Interfaces;
using KlangHub.Classes;
using KlangHub.Platform.Audio;
using KlangHub.Streaming;

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
        private IAudioEncoder? encoder = null;
        private SupportedStreamFormat streamFormatSelected = SupportedStreamFormat.Wav_32bit; // max-quality lossless out-of-box; settings override on load

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

            // WAV/LPCM streams pass raw PCM straight through; compressed/lossless-coded formats (MP3, FLAC) run
            // through their encoder. StreamCodec is the single source of truth for that decision.
            if (!StreamCodec.IsWav(streamFormatSelected))
            {
                if (encoder == null)
                {
                    encoder = CreateEncoder(streamFormatSelected, formatIn);
                }
                // Tier2-A1: dataToSendIn is already frame.Data (a fresh per-frame array read synchronously
                // by the encoder) — the old .ToArray() clone was pure waste.
                encoder.Encode(dataToSendIn);
                dataToSendIn = encoder.Read();
            }
            if (dataToSendIn.Length > 0)
            {
                devices.OnRecordingDataAvailable(dataToSendIn, formatIn, reduceLagThreshold, streamFormatSelected);
            }
        }

        private IAudioEncoder CreateEncoder(SupportedStreamFormat format, AudioFormat formatIn)
        {
            return StreamCodec.IsFlac(format)
                ? new FlacEncoder(formatIn, logger)
                : new Mp3Encoder(formatIn, format, logger);
        }

        /// <summary>A device opened an HTTP connection: either it pulls the audio stream, or it fetches the
        /// branded now-playing artwork the receiver shows on screen.</summary>
        public void AcceptStreamingConnection(Socket socketIn, string httpRequestIn)
        {
            if (devices == null)
                return;

            if (ArtworkHttp.IsArtworkRequest(httpRequestIn))
            {
                ServeArtwork(socketIn);
                return;
            }

            logger.Log(string.Format("Connection added from {0}", socketIn.RemoteEndPoint));
            devices.AddStreamingConnection(socketIn, httpRequestIn, streamFormatSelected);
        }

        private void ServeArtwork(Socket socketIn)
        {
            try
            {
                socketIn.Send(ArtworkHttp.BuildImageResponse(ArtworkImage.Bytes));
            }
            catch (Exception ex)
            {
                logger.Log(ex, "ChromecastAudioSink.ServeArtwork");
            }
            finally
            {
                try { socketIn.Shutdown(SocketShutdown.Both); } catch (Exception) { }
                try { socketIn.Close(); } catch (Exception) { }
            }
        }

        public void SetStreamFormat(SupportedStreamFormat formatIn)
        {
            if (devices == null)
                return;

            if (formatIn != streamFormatSelected)
            {
                logger.Log($"Set stream format to {formatIn}");
                streamFormatSelected = formatIn;
                encoder = null;

                devices.Stop();
                devices.Start();
            }
        }

        public void ClearEncoder() => encoder = null;

        public void SetLagThreshold(int lagThresholdIn) => reduceLagThreshold = lagThresholdIn;

        public void DisposeEncoder() => encoder?.Dispose();
    }
}
