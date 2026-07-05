using KlangHub.Core.Models;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class StreamCodecTests
    {
        [Theory]
        [InlineData(SupportedStreamFormat.Wav, "audio/wav")]
        [InlineData(SupportedStreamFormat.Wav_16bit, "audio/wav")]
        [InlineData(SupportedStreamFormat.Wav_24bit, "audio/wav")]
        [InlineData(SupportedStreamFormat.Wav_32bit, "audio/wav")]
        [InlineData(SupportedStreamFormat.Mp3_128, "audio/mpeg")]
        [InlineData(SupportedStreamFormat.Mp3_320, "audio/mpeg")]
        [InlineData(SupportedStreamFormat.Flac, "audio/flac")]
        public void ContentType_matches_the_codec(SupportedStreamFormat format, string expected)
        {
            Assert.Equal(expected, StreamCodec.ContentType(format));
        }

        [Theory]
        [InlineData(SupportedStreamFormat.Wav, true)]
        [InlineData(SupportedStreamFormat.Wav_16bit, true)]
        [InlineData(SupportedStreamFormat.Wav_24bit, true)]
        [InlineData(SupportedStreamFormat.Wav_32bit, true)]
        [InlineData(SupportedStreamFormat.Mp3_128, false)]
        [InlineData(SupportedStreamFormat.Mp3_320, false)]
        [InlineData(SupportedStreamFormat.Flac, false)]
        public void IsWav_true_only_for_wav_variants(SupportedStreamFormat format, bool expected)
        {
            Assert.Equal(expected, StreamCodec.IsWav(format));
        }

        [Theory]
        [InlineData(SupportedStreamFormat.Mp3_128, true)]
        [InlineData(SupportedStreamFormat.Mp3_320, true)]
        [InlineData(SupportedStreamFormat.Wav_16bit, false)]
        [InlineData(SupportedStreamFormat.Flac, false)]
        public void IsMp3_true_only_for_mp3_variants(SupportedStreamFormat format, bool expected)
        {
            Assert.Equal(expected, StreamCodec.IsMp3(format));
        }

        [Theory]
        [InlineData(SupportedStreamFormat.Flac, true)]
        [InlineData(SupportedStreamFormat.Wav_16bit, false)]
        [InlineData(SupportedStreamFormat.Mp3_320, false)]
        public void IsFlac_true_only_for_flac(SupportedStreamFormat format, bool expected)
        {
            Assert.Equal(expected, StreamCodec.IsFlac(format));
        }

        [Theory]
        [InlineData(SupportedStreamFormat.Wav_16bit, true)]
        [InlineData(SupportedStreamFormat.Wav_24bit, true)]
        [InlineData(SupportedStreamFormat.Flac, true)]
        [InlineData(SupportedStreamFormat.Mp3_128, false)]
        [InlineData(SupportedStreamFormat.Mp3_320, false)]
        public void IsLossless_true_for_wav_and_flac(SupportedStreamFormat format, bool expected)
        {
            Assert.Equal(expected, StreamCodec.IsLossless(format));
        }
    }
}
