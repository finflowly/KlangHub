using System;
using Google.Protobuf;
using KlangHub.ProtocolBuffer;
using KlangHub.Communication.Interfaces;

namespace KlangHub.Communication
{
    /// <summary>
    /// Reassembles the Cast control channel: a stream of protobuf messages, each behind a four-byte
    /// big-endian length.
    /// <para>
    /// One instance belongs to one <see cref="DeviceConnection"/> and lives exactly as long as it does,
    /// which is what lets it hold the remainder of a read until the rest of the frame turns up.
    /// </para>
    /// <para>
    /// The version this replaces treated every read as if it were a whole number of messages. It was
    /// wrong in four ways, all of them reachable from the network and all of them locked down by tests
    /// in <c>DeviceReceiveBufferTests</c>:
    /// </para>
    /// <list type="bullet">
    /// <item>A frame that had not fully arrived left the loop counter untouched, so the loop spun on the
    /// same four bytes forever - a core at 100% for as long as the process lived. The socket hands us
    /// 2048 bytes at a time and kept no remainder between reads, so this needed no attacker: a status
    /// message past the buffer size, or TCP splitting one anywhere, was enough.</item>
    /// <item>The body was always cut from offset 4 instead of from the current offset, so the second and
    /// every later message in a single read was read from the wrong place and silently dropped.</item>
    /// <item>A negative length passed the completeness check (<c>4 + -1</c> is smaller than any buffer)
    /// and reached <c>new byte[-1]</c>.</item>
    /// <item><see cref="int.MaxValue"/> made <c>4 + length</c> overflow to a negative number, so that
    /// check passed too, and a 2 GB allocation was attempted.</item>
    /// </list>
    /// <para>
    /// The last two mattered more than they look: this runs on an I/O completion callback with no
    /// <c>catch</c> above it, so the exception did not fail a message - it ended KlangHub. Four bytes
    /// from anything on the network that answered on port 8009 were enough.
    /// </para>
    /// </summary>
    public class DeviceReceiveBuffer : IDeviceReceiveBuffer
    {
        /// <summary>
        /// The largest frame this will assemble, matching the limit the Cast protocol itself uses
        /// (<c>kMaxMessageSize</c>, 64 KiB, in the Chromium cast_channel implementation). A length past
        /// it cannot be a message we are meant to read, so believing it would only mean allocating on
        /// behalf of whatever sent it.
        /// </summary>
        private const int MaxFrameBytes = 64 * 1024;

        private const int LengthPrefixBytes = 4;

        /// <summary>
        /// What has arrived and not yet been consumed. Never grows past one length prefix plus one
        /// largest-allowed frame, because a length outside that range is refused before anything is kept
        /// for it.
        /// </summary>
        private byte[] pending = Array.Empty<byte>();

        private Action<CastMessage>? onReceiveMessage;

        /// <summary>
        /// Received data from a device.
        /// </summary>
        /// <param name="data">the received data</param>
        public void OnReceive(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            Append(data);
            ParseMessages();
        }

        private void Append(byte[] data)
        {
            var combined = new byte[pending.Length + data.Length];
            Buffer.BlockCopy(pending, 0, combined, 0, pending.Length);
            Buffer.BlockCopy(data, 0, combined, pending.Length, data.Length);
            pending = combined;
        }

        /// <summary>
        /// Hand over every complete message in the buffer and keep the remainder for the next read.
        /// </summary>
        private void ParseMessages()
        {
            var offset = 0;

            while (pending.Length - offset >= LengthPrefixBytes)
            {
                var frameSize = ReadBigEndianInt32(pending, offset);

                if (frameSize == 0)
                {
                    // Carries nothing, but the stream behind it is still framed correctly. Stopping here
                    // - which is what the inherited parser did - discarded everything queued after it.
                    offset += LengthPrefixBytes;
                    continue;
                }

                if (frameSize < 0 || frameSize > MaxFrameBytes)
                {
                    // Framing is lost: without a trustworthy length there is no way to find where the
                    // next message starts, so there is nothing to resynchronise to. Drop what we hold
                    // rather than spin or allocate on it. The connection's own silence check gives up on
                    // a device that stops making sense and builds the connection again.
                    pending = Array.Empty<byte>();
                    return;
                }

                if (pending.Length - offset - LengthPrefixBytes < frameSize)
                    break; // The rest is still on its way. Keep it and wait for the next read.

                var message = new byte[frameSize];
                Buffer.BlockCopy(pending, offset + LengthPrefixBytes, message, 0, frameSize);
                ProcessMessage(message);

                offset += LengthPrefixBytes + frameSize;
            }

            KeepRemainderFrom(offset);
        }

        private void KeepRemainderFrom(int offset)
        {
            if (offset == 0)
                return;

            var remaining = pending.Length - offset;
            var remainder = new byte[remaining];
            Buffer.BlockCopy(pending, offset, remainder, 0, remaining);
            pending = remainder;
        }

        /// <summary>
        /// The length prefix is big-endian, which is the opposite of what this machine is.
        /// </summary>
        private static int ReadBigEndianInt32(byte[] data, int offset)
            => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

        /// <summary>
        /// Process a message.
        /// </summary>
        /// <param name="message">the message</param>
        private void ProcessMessage(byte[] message)
        {
            CastMessage castMessage;
            try
            {
                castMessage = CastMessage.Parser.ParseFrom(message);
            }
            catch (InvalidProtocolBufferException)
            {
                // One unreadable message. The framing around it was sound, so the stream carries on.
                return;
            }

            try
            {
                onReceiveMessage?.Invoke(castMessage);
            }
            catch (Exception)
            {
                // Deliberately kept, and deliberately separate from the parse above. Everything this
                // reaches - status handling, the UI - runs on an I/O completion callback with no catch
                // between here and the top of the thread, so anything that escapes ends the process
                // rather than the message. Narrow it only together with a handler that can report.
            }
        }

        /// <summary>
        /// Set the callback.
        /// </summary>
        public void SetCallback(Action<CastMessage> onReceiveMessageIn)
        {
            onReceiveMessage = onReceiveMessageIn;
        }
    }
}
