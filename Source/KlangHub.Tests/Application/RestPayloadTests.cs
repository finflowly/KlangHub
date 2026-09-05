using System.Text;
using System.Text.Json;
using KlangHub.Rest;
using Xunit;

namespace KlangHub.Tests.Application
{
    /// <summary>
    /// The bytes the REST API puts on the wire.
    /// <para>
    /// Both halves used to be built by hand. The body concatenated a device's friendly name straight into
    /// JSON - and that name comes from the device's own mDNS announcement, so anybody on the network
    /// chooses it. The header then declared a <c>Content-Length</c> counted in <em>characters</em> while
    /// the body was sent as UTF-8 bytes, so one umlaut in a speaker's name truncated the reply and left
    /// the remainder sitting in the stream for whatever was read next.
    /// </para>
    /// </summary>
    public class RestPayloadTests
    {
        [Fact]
        public void A_device_name_with_a_quote_in_it_does_not_break_the_reply()
        {
            // The name is whatever the device announced. `Bad" , "x` would have closed the string and
            // opened a field of the sender's choosing.
            var json = RestPayload.DeviceList(new[]
            {
                new RestDevice("Bad\" , \"x", "Playing", "40", "192.0.2.10", "8009", "False")
            });

            var parsed = JsonDocument.Parse(json);
            var name = parsed.RootElement.GetProperty("data")[0].GetProperty("attributes").GetProperty("name").GetString();
            Assert.Equal("Bad\" , \"x", name);
        }

        [Fact]
        public void A_requested_path_is_echoed_back_as_data_not_as_syntax()
        {
            // Every reply repeats the action it was asked for. It arrives URL-decoded from the network.
            var json = RestPayload.Done("/volume/a\"b/50");

            var parsed = JsonDocument.Parse(json);
            Assert.Equal("/volume/a\"b/50",
                parsed.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("action").GetString());
        }

        [Fact]
        public void The_declared_length_is_the_number_of_bytes_actually_sent()
        {
            // "Küche" is five characters and six bytes. Declaring five truncated the reply by one.
            var body = RestPayload.DeviceList(new[]
            {
                new RestDevice("Küche", "Idle", "20", "192.0.2.11", "8009", "False")
            });

            var wire = Encoding.UTF8.GetString(RestPayload.Http(ok: true, body));
            var declared = int.Parse(HeaderValue(wire, "Content-Length"));
            var sentBody = wire[(wire.IndexOf("\r\n\r\n") + 4)..];

            Assert.Equal(Encoding.UTF8.GetByteCount(sentBody), declared);
            Assert.Equal(body, sentBody);
        }

        [Fact]
        public void A_refusal_says_so_in_its_status_line()
        {
            var wire = Encoding.UTF8.GetString(RestPayload.Http(ok: false, "{}"));

            Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", wire);
        }

        [Fact]
        public void An_answer_says_so_in_its_status_line()
        {
            var wire = Encoding.UTF8.GetString(RestPayload.Http(ok: true, "{}"));

            Assert.StartsWith("HTTP/1.1 200 OK\r\n", wire);
        }

        [Fact]
        public void The_shape_of_a_device_list_is_unchanged()
        {
            // Anything already scripted against this API keeps working.
            var json = RestPayload.DeviceList(new[]
            {
                new RestDevice("Speaker", "Playing", "40", "192.0.2.10", "8009", "True")
            });

            var attributes = JsonDocument.Parse(json).RootElement
                .GetProperty("data")[0].GetProperty("attributes");

            Assert.Equal("40", attributes.GetProperty("volume").GetString());
            Assert.Equal("192.0.2.10", attributes.GetProperty("ip").GetString());
            Assert.Equal("8009", attributes.GetProperty("port").GetString());
            Assert.Equal("True", attributes.GetProperty("isgroup").GetString());
            Assert.Equal("device", JsonDocument.Parse(json).RootElement.GetProperty("data")[0].GetProperty("type").GetString());
        }

        private static string HeaderValue(string wire, string name)
        {
            foreach (var line in wire[..wire.IndexOf("\r\n\r\n")].Split("\r\n"))
                if (line.StartsWith(name + ":"))
                    return line[(name.Length + 1)..].Trim();

            throw new Xunit.Sdk.XunitException($"no {name} header in the reply");
        }
    }
}
