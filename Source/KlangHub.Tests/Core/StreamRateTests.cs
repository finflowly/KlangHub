using KlangHub.Core.Audio;
using KlangHub.Core.Models;
using Xunit;

namespace KlangHub.Tests.Core
{
    /// <summary>
    /// The byte rate every "buffer in seconds" calculation depends on. It was guessed with two constants
    /// before, and the guess was only right for MP3 - which is why a ten-second setting meant two seconds of
    /// WAV and thirty-two seconds of MP3 128 before the first byte was sent. The receiver reloaded instead of
    /// waiting, and the television showed its spinner two or three times before playback finally started.
    /// </summary>
    public class StreamRateTests
    {
        private static readonly AudioFormat Cd = new AudioFormat(44100, 16, 2);      // 176 400 B/s
        private static readonly AudioFormat Studio = new AudioFormat(48000, 24, 2);  // 288 000 B/s

        [Fact]
        public void Wav_is_exactly_the_pcm_rate()
        {
            Assert.Equal(176_400, StreamRate.BytesPerSecond(Cd, SupportedStreamFormat.Wav_16bit));
            Assert.Equal(288_000, StreamRate.BytesPerSecond(Studio, SupportedStreamFormat.Wav_24bit));
        }

        [Theory]
        [InlineData(SupportedStreamFormat.Mp3_320, 40_000)]
        [InlineData(SupportedStreamFormat.Mp3_128, 16_000)]
        public void Mp3_is_its_bitrate(SupportedStreamFormat format, double expected)
        {
            // independent of the PCM format: the encoder produces a constant bitrate
            Assert.Equal(expected, StreamRate.BytesPerSecond(Cd, format));
            Assert.Equal(expected, StreamRate.BytesPerSecond(Studio, format));
        }

        [Fact]
        public void Flac_is_a_fraction_of_the_pcm_rate()
        {
            var rate = StreamRate.BytesPerSecond(Cd, SupportedStreamFormat.Flac);

            Assert.True(rate < Cd.AverageBytesPerSecond, "lossless compression must be smaller than PCM");
            Assert.True(rate > Cd.AverageBytesPerSecond / 2.0, "but not as small as a lossy codec");
        }

        [Fact]
        public void Ten_seconds_of_mp3_is_seconds_of_waiting_not_half_a_minute()
        {
            // the regression in numbers: the old threshold for MP3 128 was 510 000 bytes, which is 32 s of
            // audio. Sized correctly it is a tenth of that.
            var bytes = StreamRate.BytesForSeconds(Cd, SupportedStreamFormat.Mp3_128, 10);

            Assert.Equal(160_000, bytes);
            Assert.Equal(10, bytes / StreamRate.BytesPerSecond(Cd, SupportedStreamFormat.Mp3_128));
        }

        [Fact]
        public void Seconds_mean_the_same_thing_in_every_format()
        {
            foreach (var format in new[]
                     {
                         SupportedStreamFormat.Wav_16bit, SupportedStreamFormat.Wav_24bit,
                         SupportedStreamFormat.Flac, SupportedStreamFormat.Mp3_320, SupportedStreamFormat.Mp3_128,
                     })
            {
                var seconds = StreamRate.BytesForSeconds(Cd, format, 7) / StreamRate.BytesPerSecond(Cd, format);
                Assert.Equal(7, seconds, 6);
            }
        }

        [Fact]
        public void A_missing_format_does_not_divide_by_zero()
        {
            Assert.True(StreamRate.BytesPerSecond(null!, SupportedStreamFormat.Wav_16bit) > 0);
        }
    }
}
