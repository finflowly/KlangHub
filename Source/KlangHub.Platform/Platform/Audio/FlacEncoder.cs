using System;
using CUETools.Codecs;
using CUETools.Codecs.FLAKE;

namespace KlangHub.Platform.Audio
{
    /// <summary>
    /// Lossless FLAC live-encoder behind <see cref="IAudioEncoder"/>. Wraps FLAKE's managed
    /// <c>FlakeWriter</c> feeding a <see cref="NonSeekableForwardingStream"/>, so the FLAC header + frames are
    /// produced sequentially with an unknown total length — the shape a Chromecast pulls as an endless stream.
    ///
    /// Input is integer PCM matching <see cref="AudioFormat"/> (the capture engine forces 16-bit int for the
    /// FLAC format; FLAC needs integer, not float, samples). <see cref="Encode"/> hands whole PCM frames to the
    /// writer; <see cref="Read"/> drains the FLAC bytes emitted so far into the streaming pipeline.
    /// </summary>
    public sealed class FlacEncoder : IAudioEncoder
    {
        // FLAC compression 0..8 (FLAKE also allows up to 11). 5 is a safe real-time default on a desktop CPU —
        // far faster than real-time for stereo 44.1/48 kHz — while giving strong compression.
        public const int DefaultCompressionLevel = 5;

        private readonly NonSeekableForwardingStream sink;
        private readonly FlakeWriter writer;
        private readonly AudioPCMConfig pcm;
        private readonly int blockAlign;
        private readonly ILogger logger;
        private bool disposed;

        public FlacEncoder(AudioFormat format, ILogger logger, int compressionLevel = DefaultCompressionLevel)
        {
            this.logger = logger;
            pcm = new AudioPCMConfig(format.BitsPerSample, format.Channels, format.SampleRate);
            blockAlign = pcm.BlockAlign; // channels * bytes-per-sample
            sink = new NonSeekableForwardingStream();
            writer = new FlakeWriter(null, sink, pcm) { CompressionLevel = compressionLevel };
        }

        /// <summary>Feed raw integer-PCM bytes; whole frames are encoded, any trailing partial frame is skipped
        /// (WASAPI delivers frame-aligned buffers, so this is defensive only).</summary>
        public void Encode(byte[] pcmBytes)
        {
            if (pcmBytes == null || pcmBytes.Length == 0 || disposed)
                return;

            try
            {
                int frames = pcmBytes.Length / blockAlign;
                if (frames <= 0)
                    return;

                var buffer = new AudioBuffer(pcm, pcmBytes, frames);
                writer.Write(buffer);
            }
            catch (Exception ex)
            {
                logger.Log(ex, "FlacEncoder.Encode");
            }
        }

        public byte[] Read() => sink.Drain();

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            try
            {
                writer.Close(); // flush the trailing partial block into the (still-drainable) sink
            }
            catch (Exception ex)
            {
                logger.Log(ex, "FlacEncoder.Dispose");
            }
        }
    }
}
