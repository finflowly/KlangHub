using KlangHub.Core.Audio;
using KlangHub.Platform.Audio;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class WaveFormatMappingTests
    {
        [Theory]
        [InlineData(44100, 16, 2)]
        [InlineData(48000, 24, 2)]
        [InlineData(48000, 32, 2)]
        [InlineData(44100, 16, 1)]
        [InlineData(32000, 16, 2)]
        public void AudioFormat_roundtrips_through_WaveFormat(int sampleRate, int bits, int channels)
        {
            var original = new AudioFormat(sampleRate, bits, channels);

            var roundtripped = original.ToWaveFormat().ToAudioFormat();

            Assert.Equal(original, roundtripped); // AudioFormat is a record -> value equality
        }

        [Fact]
        public void ToWaveFormat_carries_the_pcm_triple()
        {
            var wf = new AudioFormat(48000, 24, 2).ToWaveFormat();

            Assert.Equal(48000, wf.SampleRate);
            Assert.Equal(24, wf.BitsPerSample);
            Assert.Equal(2, wf.Channels);
        }
    }
}
