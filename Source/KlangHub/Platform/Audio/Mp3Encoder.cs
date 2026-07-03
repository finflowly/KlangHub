using KlangHub.Application.Interfaces;
using KlangHub.Classes;
using NAudio.Wave;

namespace KlangHub.Platform.Audio
{
    /// <summary>
    /// Thin adapter that exposes the existing LAME-based <see cref="Mp3Stream"/> as an
    /// <see cref="IAudioEncoder"/>. Wrap, don't rewrite: all encoding logic stays in Mp3Stream;
    /// this class only confines the NAudio dependency to the Platform layer.
    /// </summary>
    public sealed class Mp3Encoder : IAudioEncoder
    {
        private readonly Mp3Stream inner;

        public Mp3Encoder(WaveFormat format, SupportedStreamFormat streamFormat, ILogger logger)
        {
            inner = new Mp3Stream(format, streamFormat, logger);
        }

        public void Encode(byte[] wav) => inner.Encode(wav);

        public byte[] Read() => inner.Read();

        public void Dispose() => inner.Dispose();
    }
}
