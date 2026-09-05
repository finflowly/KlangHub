using System;
using System.Collections.Generic;

namespace KlangHub.Core.Streaming
{
    /// <summary>
    /// The last few seconds of audio, kept so a device that starts playing has something to begin with
    /// instead of silence.
    /// <para>
    /// It sits on the capture path: at 24-bit 48 kHz stereo that is some 288 kB a second arriving in
    /// blocks of a few kilobytes, so what it costs per block matters. The version this replaces was a
    /// <c>List</c> that inserted every new block at index 0 - shifting every element along - and then, to
    /// decide whether to trim, summed the length of every block in the list <em>on each iteration of the
    /// trim loop</em>. All of it inside the lock the streaming path also needs.
    /// </para>
    /// <para>
    /// Reading it back was worse: it built one <c>Concat</c> enumerator per block, nested, and unwound
    /// them recursively at the end. With a few thousand blocks that is not slow, it is a stack overflow.
    /// </para>
    /// <para>
    /// Now: append to a queue, keep a running total, drop from the front. One copy per block, and the
    /// arithmetic is a subtraction.
    /// </para>
    /// </summary>
    public sealed class AudioRingBuffer
    {
        private readonly object gate = new object();
        private readonly Queue<byte[]> blocks = new Queue<byte[]>();
        private long byteCount;
        private long capacityBytes;

        public AudioRingBuffer(long capacityBytes)
        {
            this.capacityBytes = Math.Max(0, capacityBytes);
        }

        /// <summary>
        /// How much audio to keep. The user can change the extra-seconds setting while music is playing,
        /// so this is not fixed at construction; a smaller value takes effect as the next block arrives.
        /// </summary>
        public long CapacityBytes
        {
            get { lock (gate) return capacityBytes; }
            set { lock (gate) capacityBytes = Math.Max(0, value); }
        }

        /// <summary>How much is being held.</summary>
        public long ByteCount
        {
            get { lock (gate) return byteCount; }
        }

        /// <summary>Add the block that has just been captured, and drop as much old audio as that costs.</summary>
        public void Add(byte[] block)
        {
            if (block == null || block.Length == 0)
                return;

            lock (gate)
            {
                blocks.Enqueue(block);
                byteCount += block.Length;

                // The oldest block is what goes. Its own length is left out of the comparison so that a
                // single block larger than the whole cushion is still kept: handing a device nothing at
                // all is the one outcome this buffer exists to prevent.
                while (blocks.Count > 0 && byteCount - blocks.Peek().Length > capacityBytes)
                    byteCount -= blocks.Dequeue().Length;
            }
        }

        /// <summary>Everything held, oldest first - the order it was played in.</summary>
        public byte[] ToArray()
        {
            lock (gate)
            {
                if (byteCount == 0)
                    return Array.Empty<byte>();

                var result = new byte[byteCount];
                var at = 0;
                foreach (var block in blocks)
                {
                    Buffer.BlockCopy(block, 0, result, at, block.Length);
                    at += block.Length;
                }

                return result;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                blocks.Clear();
                byteCount = 0;
            }
        }
    }
}
