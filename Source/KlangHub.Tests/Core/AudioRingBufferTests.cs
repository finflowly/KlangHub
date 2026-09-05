using System;
using System.Linq;
using System.Threading.Tasks;
using KlangHub.Core.Streaming;
using Xunit;

namespace KlangHub.Tests.Core
{
    /// <summary>
    /// The cushion of recent audio a device is handed when it starts playing.
    /// <para>
    /// It is written on the capture thread and read whenever a device joins, which at 24-bit 48 kHz is
    /// some 288 kB every second in blocks of a few kilobytes. The version this replaces was a
    /// <c>List</c> that inserted each new block at index 0 - moving every element along - and then
    /// summed the length of every block in the list on <em>each iteration</em> of the trim loop, all
    /// inside the lock the streaming path needs. Reading it back built one <c>Concat</c> enumerator per
    /// block and unwound them recursively, which for a few thousand blocks is a stack overflow waiting
    /// for a slow afternoon.
    /// </para>
    /// </summary>
    public class AudioRingBufferTests
    {
        private static byte[] Block(byte value, int length) => Enumerable.Repeat(value, length).ToArray();

        [Fact]
        public void What_goes_in_comes_out_in_the_order_it_arrived()
        {
            var buffer = new AudioRingBuffer(1000);
            buffer.Add(new byte[] { 1, 2 });
            buffer.Add(new byte[] { 3, 4 });
            buffer.Add(new byte[] { 5 });

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer.ToArray());
        }

        [Fact]
        public void An_empty_buffer_hands_back_nothing_rather_than_null()
        {
            Assert.Empty(new AudioRingBuffer(1000).ToArray());
        }

        [Fact]
        public void The_oldest_audio_is_what_goes_when_the_cushion_is_full()
        {
            var buffer = new AudioRingBuffer(10);
            buffer.Add(Block(1, 8));
            buffer.Add(Block(2, 8));
            buffer.Add(Block(3, 8));

            // Whatever survives, it is the most recent audio - a cushion of stale sound is worse than a
            // short one.
            var kept = buffer.ToArray();
            Assert.Equal(3, kept[^1]);
            Assert.DoesNotContain((byte)1, kept);
        }

        [Fact]
        public void A_single_block_larger_than_the_cushion_is_still_kept()
        {
            // Otherwise a device joining would be handed nothing at all, which is the one outcome the
            // cushion exists to prevent.
            var buffer = new AudioRingBuffer(10);
            buffer.Add(Block(7, 5000));

            Assert.Equal(5000, buffer.ToArray().Length);
        }

        [Fact]
        public void Clearing_it_empties_it()
        {
            var buffer = new AudioRingBuffer(1000);
            buffer.Add(Block(1, 100));
            buffer.Clear();

            Assert.Empty(buffer.ToArray());
            Assert.Equal(0, buffer.ByteCount);
        }

        [Fact]
        public void A_smaller_cushion_takes_effect_on_the_next_block()
        {
            // The user can change the extra-seconds setting while audio is playing.
            var buffer = new AudioRingBuffer(10_000);
            for (var i = 0; i < 10; i++)
                buffer.Add(Block(1, 1000));

            buffer.CapacityBytes = 2000;
            buffer.Add(Block(2, 1000));

            Assert.True(buffer.ByteCount <= 3000, $"still holding {buffer.ByteCount} bytes");
        }

        [Fact]
        public void Nothing_added_is_nothing_kept()
        {
            var buffer = new AudioRingBuffer(1000);
            buffer.Add(null!);
            buffer.Add(Array.Empty<byte>());

            Assert.Equal(0, buffer.ByteCount);
        }

        [Fact]
        public async Task Reading_while_writing_never_hands_back_a_torn_block()
        {
            // The capture thread adds while a device joining reads. A half-copied array would be audible.
            var buffer = new AudioRingBuffer(64 * 1024);
            using var stop = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1));

            var writing = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                    buffer.Add(Block(9, 1024));
            }, TestContext.Current.CancellationToken);

            var reads = 0;
            while (!stop.IsCancellationRequested && reads < 500)
            {
                var snapshot = buffer.ToArray();
                Assert.True(snapshot.All(b => b == 9 || b == 0), "a block came back half-written");
                reads++;
            }

            stop.Cancel();
            await writing;
        }
    }
}
