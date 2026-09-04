using System;
using KlangHub.Core.Audio;

namespace KlangHub.Core.Models
{
    /// <summary>
    /// How many bytes per second a stream format actually produces - the number every "buffer in seconds"
    /// calculation needs.
    /// <para>
    /// It used to be guessed with two constants: 40 000 B/s for WAV, MP3 320 and FLAC alike, 16 000 for
    /// MP3 128. Only the MP3 numbers were right. Real WAV runs at 192 000 B/s (16-bit/48 kHz stereo), so a
    /// "10 second" start-up buffer was really two - while for MP3 128 the same setting meant waiting for
    /// 32 seconds of audio before the first byte left the machine. The receiver gave up long before that and
    /// reloaded, which is exactly the "spinner goes round two or three times, then it plays" symptom.
    /// </para>
    /// </summary>
    public static class StreamRate
    {
        /// <summary>
        /// FLAC on real music lands around 50-70 % of the PCM size. The high end is used on purpose: it is
        /// better to over-estimate the rate (and buffer a little less audio) than to under-estimate it and
        /// make the listener wait.
        /// </summary>
        public const double FlacCompressionRatio = 0.7;

        public static double BytesPerSecond(AudioFormat format, SupportedStreamFormat streamFormat)
        {
            if (format == null)
                return 40000;   // a sane fallback rather than a division by zero further up

            switch (streamFormat)
            {
                case SupportedStreamFormat.Mp3_320:
                    return 320_000 / 8.0;
                case SupportedStreamFormat.Mp3_128:
                    return 128_000 / 8.0;
                case SupportedStreamFormat.Flac:
                    return Math.Max(1, format.AverageBytesPerSecond * FlacCompressionRatio);
                default:
                    return Math.Max(1, format.AverageBytesPerSecond);   // WAV is exact: it IS the PCM rate
            }
        }

        /// <summary>Bytes needed to hold <paramref name="seconds"/> of this stream.</summary>
        public static double BytesForSeconds(AudioFormat format, SupportedStreamFormat streamFormat, double seconds) =>
            BytesPerSecond(format, streamFormat) * Math.Max(0, seconds);
    }
}
