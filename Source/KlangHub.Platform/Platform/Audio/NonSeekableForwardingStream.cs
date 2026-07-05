using System;
using System.IO;

namespace KlangHub.Platform.Audio
{
    /// <summary>
    /// A write-only, <b>non-seekable</b> sink that FLAKE's <c>FlakeWriter</c> writes the encoded FLAC stream
    /// into. Non-seekability is deliberate and load-bearing: <c>FlakeWriter</c> only patches the STREAMINFO
    /// header (total sample count, MD5) inside <c>if (_IO.CanSeek)</c>. With <see cref="CanSeek"/> = false it
    /// never seeks, so it emits the header once with <c>total_samples = 0</c> (unknown) followed by
    /// self-contained FLAC frames — exactly a valid, endless progressive FLAC stream for live casting.
    ///
    /// The bytes accumulate in an internal buffer; <see cref="Drain"/> hands them to the streaming pipeline and
    /// clears. <see cref="Close"/> is intentionally a no-op so a final <c>FlakeWriter.Close()</c> flush stays
    /// drainable.
    /// </summary>
    internal sealed class NonSeekableForwardingStream : Stream
    {
        private readonly MemoryStream sink = new MemoryStream();
        private readonly object gate = new object();

        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (gate)
            {
                sink.Write(buffer, offset, count);
            }
        }

        /// <summary>Return everything written since the last drain and reset the buffer.</summary>
        public byte[] Drain()
        {
            lock (gate)
            {
                var bytes = sink.ToArray();
                sink.SetLength(0);
                return bytes;
            }
        }

        public override void Flush() { }

        // Keep the buffer alive after FlakeWriter.Close() so the trailing flush can still be drained.
        public override void Close() { }

        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
