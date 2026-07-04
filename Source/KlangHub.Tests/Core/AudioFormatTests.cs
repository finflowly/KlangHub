using KlangHub.Core.Audio;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class AudioFormatTests
    {
        [Fact]
        public void Derived_values_are_computed_from_the_pcm_triple()
        {
            var format = new AudioFormat(48000, 24, 2);

            Assert.Equal(6, format.BlockAlign);                 // Channels * BitsPerSample/8 = 2 * 3
            Assert.Equal(288000, format.AverageBytesPerSecond); // SampleRate * BlockAlign = 48000 * 6
        }

        [Theory]
        [InlineData(44100, 16, 2, 4, 176400)]
        [InlineData(48000, 32, 2, 8, 384000)]
        [InlineData(44100, 16, 1, 2, 88200)]
        [InlineData(32000, 16, 2, 4, 128000)]
        public void Derived_values_match_expected(int sampleRate, int bits, int channels, int blockAlign, int averageBytesPerSecond)
        {
            var format = new AudioFormat(sampleRate, bits, channels);

            Assert.Equal(blockAlign, format.BlockAlign);
            Assert.Equal(averageBytesPerSecond, format.AverageBytesPerSecond);
        }
    }
}
