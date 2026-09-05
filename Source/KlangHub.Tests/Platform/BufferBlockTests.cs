using KlangHub.Streaming;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// A full buffer used to swallow audio without a word. For FLAC and MP3 that is not a click, it is a
    /// broken bitstream and a receiver that stops - so the loss has to be countable.
    /// </summary>
    public class BufferBlockTests
    {
        private static BufferBlock Small() => new() { Data = new byte[10] };

        [Fact]
        public void Data_that_fits_is_stored()
        {
            var buffer = Small();
            Assert.True(buffer.Add(new byte[4], 4));
            Assert.Equal(4, buffer.Used);
            Assert.Equal(0, buffer.DroppedBytes);
        }

        [Fact]
        public void Data_that_does_not_fit_is_counted_rather_than_lost_in_silence()
        {
            var buffer = Small();
            buffer.Add(new byte[8], 8);

            Assert.False(buffer.Add(new byte[5], 5));
            Assert.Equal(5, buffer.DroppedBytes);
            Assert.Equal(8, buffer.Used);      // what was already there stays intact
        }

        [Fact]
        public void Losses_accumulate_until_somebody_reads_them()
        {
            var buffer = Small();
            buffer.Add(new byte[9], 9);
            buffer.Add(new byte[5], 5);
            buffer.Add(new byte[7], 7);

            Assert.Equal(12, buffer.DroppedBytes);
        }

        [Fact]
        public void Reading_the_counter_resets_it_so_a_report_covers_one_interval()
        {
            var buffer = Small();
            buffer.Add(new byte[20], 20);

            Assert.Equal(20, buffer.TakeDroppedBytes());
            Assert.Equal(0, buffer.DroppedBytes);
            Assert.Equal(0, buffer.TakeDroppedBytes());
        }

        [Fact]
        public void An_exactly_fitting_block_is_not_treated_as_a_loss()
        {
            var buffer = Small();
            Assert.True(buffer.Add(new byte[10], 10));
            Assert.Equal(0, buffer.DroppedBytes);
        }

        [Fact]
        public void A_span_from_the_capture_callback_is_copied_in_the_same_way()
        {
            // NAudio 3 hands the WASAPI buffer over as a span that is only valid inside the callback.
            var buffer = Small();
            var packet = new byte[] { 1, 2, 3, 4 };

            Assert.True(buffer.Add(new System.ReadOnlySpan<byte>(packet)));
            Assert.Equal(4, buffer.Used);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.Data[..4]);

            // and the copy is a copy: the caller's memory going away must not matter
            packet[0] = 99;
            Assert.Equal(1, buffer.Data[0]);
        }

        [Fact]
        public void A_span_that_does_not_fit_is_counted_too()
        {
            var buffer = Small();
            buffer.Add(new byte[8], 8);

            Assert.False(buffer.Add(new System.ReadOnlySpan<byte>(new byte[5])));
            Assert.Equal(5, buffer.DroppedBytes);
        }
    }
}
