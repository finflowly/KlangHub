using System;
using System.IO;
using NAudio.Lame;
using NAudio.Wave;

namespace KlangHub.Classes
{
    /// <summary>
    /// MP3 encode a WAV stream.
    /// </summary>
    public class Mp3Stream : IDisposable
    {
        /// <summary>
        /// One gate for all three operations.
        /// <para>
        /// <c>Encode</c> used to lock on <c>Writer</c> while <c>Read</c> took <c>Output</c> without any
        /// lock at all - and LAME writes into that very stream from inside the other lock. So the class
        /// held a lock that protected the wrong object and suggested a safety it did not provide; what
        /// actually kept it upright was a second lock a caller happened to hold.
        /// </para>
        /// </summary>
        private readonly object gate = new object();

        private MemoryStream? output;
        private LameMP3FileWriter? writer;
        private readonly ILogger logger;
        private bool disposed;

        /// <summary>
        /// Setup MP3 encoding with the selected WAV and stream formats.
        /// </summary>
        /// <param name="format">the WAV input format</param>
        /// <param name="formatSelected">the mp3 output format</param>
        public Mp3Stream(WaveFormat format, SupportedStreamFormat formatSelected, ILogger loggerIn)
        {
            logger = loggerIn;
            output = new MemoryStream();
            var bitRate = formatSelected.Equals(SupportedStreamFormat.Mp3_320) ? 320 : 128;

            writer = new LameMP3FileWriter(output, format, bitRate);
        }

        /// <summary>
        /// Add WAV data that should be encoded.
        /// </summary>
        public void Encode(byte[] buffer)
        {
            if (buffer == null)
                return;

            try
            {
                // Tier2-A2: LAME writes synchronously inside the lock, so the defensive buffer.ToArray()
                // clone was dead weight - write the caller's buffer directly.
                lock (gate)
                {
                    if (disposed || writer == null)
                        return;

                    writer.Write(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                logger.Log(ex, "Mp3Stream.Encode");
            }
        }

        /// <summary>
        /// Read the data that's encoded in MP3 format.
        /// </summary>
        public byte[] Read()
        {
            lock (gate)
            {
                if (disposed || output == null)
                    return Array.Empty<byte>();

                var byteArray = output.ToArray();
                output.SetLength(0);
                return byteArray;
            }
        }

        /// <summary>
        /// Release the encoder.
        /// <para>
        /// The whole body of this method used to be commented out, under a note saying that disposing the
        /// writer sometimes threw an AccessViolationException. That note was true and the reason was the
        /// locking above: LAME is native, and tearing it down while another thread is inside
        /// <c>Write</c> is not an exception, it is memory being freed underneath a running encoder.
        /// Behind one gate, and with <see cref="disposed"/> shutting the door first, there is no other
        /// thread inside it to trip over - and the encoder is actually released. Leaving it was a native
        /// LAME instance abandoned on every change of stream format.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;

                disposed = true;

                try
                {
                    writer?.Dispose();      // flushes the last frames into output on the way out
                }
                catch (Exception ex)
                {
                    logger.Log(ex, "Mp3Stream.Dispose(writer)");
                }
                finally
                {
                    writer = null;
                }

                try
                {
                    output?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Log(ex, "Mp3Stream.Dispose(output)");
                }
                finally
                {
                    output = null;
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
