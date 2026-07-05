namespace KlangHub.Core.Models
{
    /// <summary>
    /// Single source of truth for how a <see cref="SupportedStreamFormat"/> maps to a codec: the HTTP
    /// <c>Content-Type</c>, the Cast LOAD <c>contentType</c>, and encoder selection all resolve here so they
    /// can never disagree (the old code hardcoded <c>audio/wav</c> in two places even for MP3 streams).
    /// </summary>
    public static class StreamCodec
    {
        public static bool IsWav(SupportedStreamFormat format)
        {
            switch (format)
            {
                case SupportedStreamFormat.Wav:
                case SupportedStreamFormat.Wav_16bit:
                case SupportedStreamFormat.Wav_24bit:
                case SupportedStreamFormat.Wav_32bit:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMp3(SupportedStreamFormat format)
        {
            return format == SupportedStreamFormat.Mp3_128 || format == SupportedStreamFormat.Mp3_320;
        }

        public static bool IsFlac(SupportedStreamFormat format)
        {
            return format == SupportedStreamFormat.Flac;
        }

        /// <summary>True for bit-perfect codecs (uncompressed WAV/LPCM and lossless FLAC).</summary>
        public static bool IsLossless(SupportedStreamFormat format)
        {
            return IsWav(format) || IsFlac(format);
        }

        /// <summary>The MIME type sent both in the streaming server's HTTP header and the Cast LOAD payload.</summary>
        public static string ContentType(SupportedStreamFormat format)
        {
            if (IsMp3(format))
                return "audio/mpeg";
            if (IsFlac(format))
                return "audio/flac";
            return "audio/wav";
        }
    }
}
