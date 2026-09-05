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

        /// <summary>
        /// Guards the encoder. An encoder is a state machine carrying a half-filled block between calls, and
        /// FLAKE reaches into its buffers through unsafe pointers - entering one from two threads does not
        /// merely garble the stream, it writes past managed arrays and corrupts the GC heap.
        ///
        /// The capture engine is now built so only one thread ever gets here, but this is the layer that can
        /// PROVE it, and it costs an uncontended lock on a path that runs at most a thousand times a second.
        /// </summary>
        private readonly object encoderSync = new();

        /// <summary>Test seam: how an encoder is made for a format. Production uses the real encoders.</summary>
        internal Func<SupportedStreamFormat, AudioFormat, IAudioEncoder>? EncoderFactory { get; set; }
        // volatile: written from the interface thread when the user changes the format, read on the
        // capture thread for every frame.
        private volatile SupportedStreamFormat streamFormatSelected = SupportedStreamFormat.Wav_24bit; // uncompressed HiFi out-of-box (verified on real hardware); settings override on load

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
            // Read once and used throughout. It was read separately for the decision, for building the
            // encoder and for the send, so a format change landing between two of those reads could have
            // built an encoder for one format and labelled its output as another.
            var format = streamFormatSelected;

            if (!StreamCodec.IsWav(format))
            {
                lock (encoderSync)
                {
                    if (encoder == null)
                    {
                        encoder = CreateEncoder(format, formatIn);
                    }
                    // Tier2-A1: dataToSendIn is already frame.Data (a fresh per-frame array read synchronously
                    // by the encoder) — the old .ToArray() clone was pure waste.
                    encoder.Encode(dataToSendIn);
                    dataToSendIn = encoder.Read();
                }
            }
            if (dataToSendIn.Length > 0)
            {
                devices.OnRecordingDataAvailable(dataToSendIn, formatIn, reduceLagThreshold, format);
            }
        }

        private IAudioEncoder CreateEncoder(SupportedStreamFormat format, AudioFormat formatIn)
        {
            if (EncoderFactory != null)
                return EncoderFactory(format, formatIn);

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
                // The real cover of the piece that is playing, when the metadata cascade found one; the
                // branded picture otherwise. Never a generic placeholder while an actual cover exists.
                var image = Platform.NowPlaying.CurrentCover.Bytes ?? ArtworkImage.Bytes;
                socketIn.Send(ArtworkHttp.BuildImageResponse(image));
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
                lock (encoderSync)
                {
                    // Disposed, not merely dropped. Letting go of an encoder without disposing it left a
                    // native LAME instance behind on every change of format.
                    encoder?.Dispose();
                    encoder = null;
                }

                devices.Stop();
                devices.Start();
            }
        }

        public void ClearEncoder()
        {
            lock (encoderSync)
                encoder = null;
        }

        public void SetLagThreshold(int lagThresholdIn) => reduceLagThreshold = lagThresholdIn;

        public void DisposeEncoder()
        {
            lock (encoderSync)
            {
                encoder?.Dispose();
                // Cleared as well as disposed. ClearEncoder, immediately above, does the clearing
                // without the disposing; this did the disposing without the clearing, and left the
                // field pointing at an encoder that had already been torn down.
                encoder = null;
            }
        }
    }
}
