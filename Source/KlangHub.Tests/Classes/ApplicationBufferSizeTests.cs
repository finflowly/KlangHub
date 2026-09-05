using KlangHub.Core.Audio;
using KlangHub.Core.Models;
using KlangHub.Classes;
using KlangHub.Streaming;
using Xunit;

namespace KlangHub.Tests.Classes
{
    /// <summary>
    /// The cushion is handed to StreamingConnection in ONE piece, and BufferBlock.Add is all-or-nothing:
    /// too big means dropped whole, not trimmed. So the size needs a ceiling as well as a floor.
    /// </summary>
    public class ApplicationBufferSizeTests
    {
        private static readonly AudioFormat Stereo24 = new(48000, 24, 2);
        private static readonly AudioFormat Surround32 = new(48000, 32, 6);

        [Fact]
        public void The_default_setting_is_what_it_says_it_is()
        {
            // 10 s extra + 2 s head-room of stereo 24-bit at 48 kHz = 288000 B/s * 12
            var bytes = ApplicationBuffer.StartupBufferBytes(Stereo24, SupportedStreamFormat.Wav_24bit, 10);
            Assert.Equal(288000d * 12, bytes);
        }

        [Fact]
        public void A_cushion_that_could_not_be_delivered_is_capped_rather_than_dropped()
        {
            // 5.1 at 32-bit is 1.15 MB/s; the maximum setting would ask for 25 MB into a 10 MB block.
            var bytes = ApplicationBuffer.StartupBufferBytes(Surround32, SupportedStreamFormat.Wav_24bit, 20);
            Assert.True(bytes <= StreamingConnection.StreamBufferBytes,
                        bytes + " bytes would be dropped whole by a " + StreamingConnection.StreamBufferBytes + " byte block");
        }

        [Fact]
        public void Even_at_the_ceiling_there_is_room_left_for_live_frames()
        {
            var bytes = ApplicationBuffer.StartupBufferBytes(Surround32, SupportedStreamFormat.Wav_24bit, 20);
            Assert.True(StreamingConnection.StreamBufferBytes - bytes > 1_000_000,
                        "the block must still hold what the capture thread adds while the cushion is in flight");
        }

        [Fact]
        public void A_tiny_format_still_gets_a_floor()
        {
            var bytes = ApplicationBuffer.StartupBufferBytes(new AudioFormat(8000, 8, 1), SupportedStreamFormat.Mp3_128, 0);
            Assert.True(bytes >= 87500, "only " + bytes + " bytes would leave the receiver hungry");
        }

        [Fact]
        public void Every_shipped_format_at_every_setting_fits()
        {
            foreach (SupportedStreamFormat format in System.Enum.GetValues<SupportedStreamFormat>())
                for (int extra = 0; extra <= 20; extra++)
                {
                    var bytes = ApplicationBuffer.StartupBufferBytes(Surround32, format, extra);
                    Assert.True(bytes <= StreamingConnection.StreamBufferBytes, format + " at " + extra + " s: " + bytes);
                }
        }
    }
}
