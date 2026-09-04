using System.Text;
using KlangHub.Core.Audio;
using KlangHub.Core.Models;
using KlangHub.Streaming;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// What goes in FRONT of the audio bytes. This is where WAV working while FLAC and MP3 stuttered came
    /// from: everything that was not WAV got a hand-built "MP3 header" in front of it - written one bit per
    /// byte, and prepended to FLAC as well, where the "fLaC" magic has to come first. These tests keep the
    /// self-describing formats clean.
    /// </summary>
    public class AudioHeaderTests
    {
        private static readonly AudioFormat Format = new AudioFormat(48000, 16, 2);

        [Theory]
        [InlineData(SupportedStreamFormat.Wav)]
        [InlineData(SupportedStreamFormat.Wav_16bit)]
        [InlineData(SupportedStreamFormat.Wav_24bit)]
        [InlineData(SupportedStreamFormat.Wav_32bit)]
        public void Wav_gets_a_riff_header(SupportedStreamFormat format)
        {
            var header = new AudioHeader().GetStreamHeader(Format, format);

            Assert.True(header.Length > 12);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(header, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(header, 8, 4));
        }

        [Theory]
        [InlineData(SupportedStreamFormat.Mp3_128)]
        [InlineData(SupportedStreamFormat.Mp3_320)]
        [InlineData(SupportedStreamFormat.Flac)]
        public void Self_describing_formats_get_nothing_in_front_of_them(SupportedStreamFormat format)
        {
            // LAME emits complete MPEG frames; the FLAC encoder writes "fLaC" + STREAMINFO itself. Any byte
            // we add here is garbage the receiver has to resynchronise past - which is what it was doing.
            Assert.Empty(new AudioHeader().GetStreamHeader(Format, format));
        }

        [Fact]
        public void A_missing_format_never_produces_junk()
        {
            Assert.Empty(new AudioHeader().GetStreamHeader(null!, SupportedStreamFormat.Wav_16bit));
        }
    }
}
