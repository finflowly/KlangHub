using KlangHub.Core.Models;
using Xunit;

namespace KlangHub.Tests.Core
{
    /// <summary>
    /// How many bytes a second of a stream weighs on the wire - and, just as important, when that question
    /// has no answer.
    /// <para>
    /// The health watch judged every stream against the <i>uncompressed</i> size of the audio, because that
    /// is what the capture format describes. For FLAC that is not a target but an upper bound: a run on
    /// 2026-09-05 sat at seventy to eighty-five per cent of it all evening, on four healthy devices, and
    /// wrote a line about it every second. Quiet music compresses further still. A number nobody can
    /// predict cannot be a threshold, so this says so instead of guessing.
    /// </para>
    /// </summary>
    public class WireRateTests
    {
        [Fact]
        public void Uncompressed_audio_weighs_exactly_what_its_format_says()
        {
            // 48 kHz, stereo, 16-bit: 48000 * 2 * 2.
            Assert.Equal(192_000, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Wav_16bit, 48_000, 2, 16));
        }

        [Fact]
        public void A_constant_bitrate_mp3_weighs_its_bitrate_whatever_the_music()
        {
            Assert.Equal(16_000, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Mp3_128, 48_000, 2, 16));
            Assert.Equal(40_000, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Mp3_320, 48_000, 2, 16));
        }

        [Fact]
        public void Flac_has_no_knowable_rate()
        {
            // Zero means "do not judge this stream by its size" - not "expect nothing".
            Assert.Equal(0, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Flac, 48_000, 2, 24));
        }

        [Fact]
        public void An_incomplete_format_has_no_knowable_rate_either()
        {
            Assert.Equal(0, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Wav_16bit, 0, 2, 16));
            Assert.Equal(0, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Wav_16bit, 48_000, 0, 16));
            Assert.Equal(0, StreamCodec.WireBytesPerSecond(SupportedStreamFormat.Wav_16bit, 48_000, 2, 0));
        }
    }
}
