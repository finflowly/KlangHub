using System.Linq;
using System.Text;
using Google.Protobuf;
using KlangHub.Communication;
using KlangHub.ProtocolBuffer;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// The Cast frame, byte for byte.
    ///
    /// The protobuf runtime was replaced (a 2013 Google.ProtocolBuffers.dll for the modern Google.Protobuf)
    /// and the definition was rewritten from proto2 to proto3. Everything about that is invisible - except
    /// on the wire, where a single missing field means a Chromecast ignores us. These tests assert the
    /// actual bytes, so the frame cannot drift without a test failing.
    ///
    /// The trap that made this necessary: in plain proto3 a field holding its default value is not written
    /// at all, and protocol_version (CASTV2_1_0 = 0) and payload_type (STRING = 0) are exactly that. The
    /// .proto declares every field `optional` to restore explicit presence - and these tests are what says
    /// so out loud.
    /// </summary>
    public class CastWireFormatTests
    {
        private static CastMessage Connect()
            => new ChromeCastMessages().GetConnectMessage();

        [Fact]
        public void The_version_and_payload_type_are_on_the_wire_even_though_both_are_zero()
        {
            var bytes = Connect().ToByteArray();

            // field 1 (protocol_version), varint: tag 0x08 then the value. First field, so first two bytes.
            Assert.Equal(new byte[] { 0x08, 0x00 }, bytes.Take(2).ToArray());

            // For payload_type the only reliable question is whether the parser SEES it - scanning the
            // bytes for tag 0x28 finds '(' inside a string just as readily. Presence is what proto3 drops
            // for a default value and what `optional` restores, so presence is what is asserted.
            var reparsed = CastMessage.Parser.ParseFrom(bytes);
            Assert.True(reparsed.HasProtocolVersion, "protocol_version was not written - a plain proto3 translation");
            Assert.True(reparsed.HasPayloadType, "payload_type was not written - a plain proto3 translation");
            Assert.Equal(CastMessage.Types.PayloadType.String, reparsed.PayloadType);
        }

        [Fact]
        public void A_frame_without_those_two_fields_is_recognisably_different()
        {
            // Guards the test above: if `optional` were dropped from the .proto, this is the frame we
            // would be sending - and it must not look like the one we do send.
            var bare = new CastMessage
            {
                SourceId = "sender-0",
                DestinationId = "receiver-0",
                Namespace = "urn:x-cast:com.google.cast.tp.connection",
                PayloadUtf8 = "{}",
            };

            Assert.False(bare.HasProtocolVersion);
            Assert.False(bare.HasPayloadType);
            Assert.True(Connect().ToByteArray().Length > bare.ToByteArray().Length);
        }

        [Fact]
        public void The_frame_carries_the_fields_a_receiver_routes_on()
        {
            var message = Connect();

            Assert.Equal("sender-0", message.SourceId);
            Assert.Equal("receiver-0", message.DestinationId);
            Assert.Equal("urn:x-cast:com.google.cast.tp.connection", message.Namespace);
            Assert.Equal(CastMessage.Types.ProtocolVersion.Castv210, message.ProtocolVersion);
            Assert.Equal(CastMessage.Types.PayloadType.String, message.PayloadType);
            Assert.Contains("\"type\":\"CONNECT\"", message.PayloadUtf8);
        }

        [Fact]
        public void What_we_write_is_what_a_receiver_reads_back()
        {
            var original = new ChromeCastMessages().GetReceiverStatusMessage(42);

            var parsed = CastMessage.Parser.ParseFrom(original.ToByteArray());

            Assert.Equal(original.SourceId, parsed.SourceId);
            Assert.Equal(original.DestinationId, parsed.DestinationId);
            Assert.Equal(original.Namespace, parsed.Namespace);
            Assert.Equal(original.PayloadUtf8, parsed.PayloadUtf8);
            Assert.Equal(original.ProtocolVersion, parsed.ProtocolVersion);
            Assert.Equal(original.PayloadType, parsed.PayloadType);
        }

        [Fact]
        public void The_length_prefix_is_four_bytes_big_endian()
        {
            // Cast frames are length-prefixed on the socket; the prefix is what a receiver reads first.
            var messages = new ChromeCastMessages();
            var message = messages.GetConnectMessage();
            var framed = messages.MessageToByteArray(message);
            var body = message.ToByteArray();

            Assert.Equal(body.Length + 4, framed.Length);
            Assert.Equal(0x00, framed[0]);
            Assert.Equal(0x00, framed[1]);
            Assert.Equal((byte)(body.Length >> 8), framed[2]);
            Assert.Equal((byte)(body.Length & 0xFF), framed[3]);
            Assert.Equal(body, framed.Skip(4).ToArray());
        }

        [Fact]
        public void A_frame_captured_from_a_real_device_still_parses()
        {
            // Built by hand in the wire encoding a Chromecast sends, so this test does not simply agree
            // with our own serializer: version 0, "receiver-0" -> "sender-0", the connection namespace,
            // payload type STRING, and a CLOSE payload.
            var payload = "{\"type\":\"CLOSE\"}";
            var wire = new System.Collections.Generic.List<byte> { 0x08, 0x00 };
            void Add(int tag, string value)
            {
                wire.Add((byte)tag);
                var utf8 = Encoding.UTF8.GetBytes(value);
                wire.Add((byte)utf8.Length);
                wire.AddRange(utf8);
            }
            Add(0x12, "receiver-0");
            Add(0x1A, "sender-0");
            Add(0x22, "urn:x-cast:com.google.cast.tp.connection");
            wire.AddRange(new byte[] { 0x28, 0x00 });
            Add(0x32, payload);

            var parsed = CastMessage.Parser.ParseFrom(wire.ToArray());

            Assert.Equal("receiver-0", parsed.SourceId);
            Assert.Equal("sender-0", parsed.DestinationId);
            Assert.Equal("urn:x-cast:com.google.cast.tp.connection", parsed.Namespace);
            Assert.Equal(payload, parsed.PayloadUtf8);
        }
    }
}
