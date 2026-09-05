using System;

namespace KlangHub.Streaming
{
    public class BufferBlock
    {
        public byte[] Data = null!;
        public int Used;

        /// <summary>
        /// How many bytes of audio this buffer had to throw away because it was full, since the last time
        /// somebody asked.
        ///
        /// It matters far more than it looks. Dropping a block out of an uncompressed WAV stream is a click;
        /// dropping one out of FLAC or MP3 breaks the bitstream, and the receiver answers with a decode
        /// error and stops - which is exactly what a Samsung soundbar did on 2026-09-05 after 88 seconds
        /// (detailedErrorCode 102). Until now the loss was silent, so a log could never show whether it had
        /// happened. Now it can.
        /// </summary>
        public long DroppedBytes { get; private set; }

        private int SpaceLeft => Data.Length - Used;

        /// <summary>Adds bytes to the buffer. Returns false when they did not fit and were lost.</summary>
        internal bool Add(byte[] dataArray, int numberOfBytes)
        {
            if (numberOfBytes > SpaceLeft)
            {
                DroppedBytes += numberOfBytes;
                return false;
            }

            Array.Copy(dataArray, 0, Data, Used, numberOfBytes);
            Used += numberOfBytes;
            return true;
        }

        /// <summary>Reads the loss counter and resets it, so a report says "since the last report".</summary>
        public long TakeDroppedBytes()
        {
            var dropped = DroppedBytes;
            DroppedBytes = 0;
            return dropped;
        }
    }
}
