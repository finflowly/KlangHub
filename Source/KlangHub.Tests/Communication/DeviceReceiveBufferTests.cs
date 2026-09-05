using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using KlangHub.Communication;
using KlangHub.ProtocolBuffer;
using Xunit;

namespace KlangHub.Tests.Communication
{
    /// <summary>
    /// The Cast control channel: a length-prefixed protobuf stream arriving over TLS.
    /// <para>
    /// Everything in this file is a message a device on the network can send, so none of it is
    /// hypothetical. The socket hands us 2048 bytes at a time (<c>DeviceConnection.bufferSize</c>) and
    /// keeps no leftovers between reads, so a frame that does not fit, or that arrives split by TCP, is
    /// the normal case rather than the exceptional one. The parser has to survive all of it: a partial
    /// frame, several frames in one read, and a length field that is nonsense - the four bytes are read
    /// straight off the wire as a signed integer, and nothing prevents a device from sending
    /// <c>FF FF FF FF</c>.
    /// </para>
    /// <para>
    /// The tests that could otherwise hang run under a timeout on purpose: the failure being locked down
    /// here is a loop that never advances, and a test suite that hangs tells you far less than one that
    /// fails.
    /// </para>
    /// </summary>
    public class DeviceReceiveBufferTests
    {
        private static readonly TimeSpan LongEnoughToBeAHang = TimeSpan.FromSeconds(2);

        /// <summary>A message shaped like the ones a device really sends.</summary>
        private static CastMessage AMessage(string payload) => new CastMessage
        {
            ProtocolVersion = CastMessage.Types.ProtocolVersion.Castv210,
            SourceId = "receiver-0",
            DestinationId = "sender-0",
            PayloadType = CastMessage.Types.PayloadType.String,
            Namespace = "urn:x-cast:com.google.cast.receiver",
            PayloadUtf8 = payload
        };

        /// <summary>The wire format: four big-endian length bytes, then the protobuf.</summary>
        private static byte[] Framed(CastMessage message)
        {
            var body = message.ToByteArray();
            var length = BitConverter.GetBytes(body.Length).Reverse().ToArray();
            return length.Concat(body).ToArray();
        }

        /// <summary>A frame header claiming a length the body does not have.</summary>
        private static byte[] FramedWithClaimedLength(int claimedLength, params byte[] body)
            => BitConverter.GetBytes(claimedLength).Reverse().Concat(body).ToArray();

        private static (DeviceReceiveBuffer buffer, List<CastMessage> received) ABuffer()
        {
            var received = new List<CastMessage>();
            var buffer = new DeviceReceiveBuffer();
            buffer.SetCallback(received.Add);
            return (buffer, received);
        }

        /// <summary>
        /// Runs the parser on another thread so a loop that never advances fails the test instead of
        /// stopping the run. Returns false when it was still going after <see cref="LongEnoughToBeAHang"/>.
        /// </summary>
        private static bool Finishes(Action work)
            => Task.Run(work).Wait(LongEnoughToBeAHang);

        [Fact]
        public void A_frame_split_across_two_reads_still_arrives()
        {
            // TCP decides where the boundaries fall, not the sender. A RECEIVER_STATUS listing several
            // applications goes past 2048 bytes easily, and then the first read holds only the beginning.
            var (buffer, received) = ABuffer();
            var wire = Framed(AMessage("{\"type\":\"RECEIVER_STATUS\"}"));
            var half = wire.Length / 2;

            Assert.True(Finishes(() =>
            {
                buffer.OnReceive(wire.Take(half).ToArray());
                buffer.OnReceive(wire.Skip(half).ToArray());
            }), "the parser never returned - an incomplete frame must not spin");

            var message = Assert.Single(received);
            Assert.Equal("{\"type\":\"RECEIVER_STATUS\"}", message.PayloadUtf8);
        }

        [Fact]
        public void Two_frames_in_one_read_both_arrive()
        {
            // A device answers a burst of GET_STATUS calls faster than we read, and both replies land in
            // the same read. Losing the second one loses a status update.
            var (buffer, received) = ABuffer();
            var wire = Framed(AMessage("{\"requestId\":1}")).Concat(Framed(AMessage("{\"requestId\":2}"))).ToArray();

            Assert.True(Finishes(() => buffer.OnReceive(wire)), "the parser never returned");

            Assert.Equal(
                new[] { "{\"requestId\":1}", "{\"requestId\":2}" },
                received.Select(m => m.PayloadUtf8).ToArray());
        }

        [Fact]
        public void A_frame_arriving_one_byte_at_a_time_still_arrives()
        {
            // The pathological end of the same problem as the split read, and the cheapest way to prove
            // the parser holds state between reads rather than hoping each one is self-contained.
            var (buffer, received) = ABuffer();
            var wire = Framed(AMessage("{\"requestId\":7}"));

            Assert.True(Finishes(() =>
            {
                foreach (var b in wire)
                    buffer.OnReceive(new[] { b });
            }), "the parser never returned");

            Assert.Equal("{\"requestId\":7}", Assert.Single(received).PayloadUtf8);
        }

        [Fact]
        public void A_negative_length_is_dropped_rather_than_thrown()
        {
            // Four bytes FF FF FF FF read as a signed big-endian integer are -1. The frame is nonsense;
            // the connection is what has to be given up on, not the process. This runs on an I/O
            // completion callback with no catch above it, so an exception here ends KlangHub.
            var (buffer, received) = ABuffer();

            Assert.True(Finishes(() => buffer.OnReceive(FramedWithClaimedLength(-1, 1, 2, 3, 4))),
                "the parser never returned");

            Assert.Empty(received);
        }

        [Fact]
        public void An_impossibly_large_length_is_dropped_rather_than_allocated()
        {
            // int.MaxValue makes `4 + messageSize` overflow to a negative number, so a check written as
            // `length >= 4 + messageSize` passes and a 2 GB allocation is attempted.
            var (buffer, received) = ABuffer();

            Assert.True(Finishes(() => buffer.OnReceive(FramedWithClaimedLength(int.MaxValue, 1, 2, 3, 4))),
                "the parser never returned");

            Assert.Empty(received);
        }

        [Fact]
        public void Rubbish_between_two_good_frames_does_not_cost_the_second_one()
        {
            // A frame whose body does not parse is dropped; the stream is still framed correctly, so the
            // message behind it must still arrive.
            var (buffer, received) = ABuffer();
            var wire = FramedWithClaimedLength(3, 0xFF, 0xFF, 0xFF)
                .Concat(Framed(AMessage("{\"requestId\":9}")))
                .ToArray();

            Assert.True(Finishes(() => buffer.OnReceive(wire)), "the parser never returned");

            Assert.Equal("{\"requestId\":9}", Assert.Single(received).PayloadUtf8);
        }

        [Fact]
        public void A_zero_length_frame_is_skipped_without_stopping_the_stream()
        {
            // A zero-length frame carries nothing. The inherited parser treated it as end-of-buffer and
            // stopped, which silently discarded everything queued behind it.
            var (buffer, received) = ABuffer();
            var wire = FramedWithClaimedLength(0).Concat(Framed(AMessage("{\"requestId\":4}"))).ToArray();

            Assert.True(Finishes(() => buffer.OnReceive(wire)), "the parser never returned");

            Assert.Equal("{\"requestId\":4}", Assert.Single(received).PayloadUtf8);
        }
    }
}
