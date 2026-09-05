using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace KlangHub.Rest
{
    /// <summary>One device, as the REST API describes it. Strings throughout, as the API always has.</summary>
    public readonly record struct RestDevice(string Name, string State, string Volume, string Ip, string Port, string IsGroup);

    /// <summary>
    /// Builds what the REST API sends back.
    /// <para>
    /// It used to be built by hand, and both halves were wrong. The body concatenated the device's
    /// friendly name straight into JSON - and that name is whatever the device announced over mDNS, so
    /// anybody on the network chooses it; a name containing a quotation mark ended the string and began
    /// a field of the sender's choosing. The header then declared a <c>Content-Length</c> counted in
    /// characters while the body went out as UTF-8 bytes, so a speaker called "Küche" made the reply one
    /// byte too short and left the remainder in the stream for whatever was read next.
    /// </para>
    /// <para>
    /// The shape of the JSON is unchanged, so anything already scripted against the API keeps working.
    /// </para>
    /// </summary>
    public static class RestPayload
    {
        private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

        /// <summary>The reply to an action: what was asked for, echoed back.</summary>
        public static string Done(string action)
            => JsonSerializer.Serialize(new
            {
                data = new { type = "done", id = "1", attributes = new { action } }
            }, Compact);

        /// <summary>The device list.</summary>
        public static string DeviceList(IEnumerable<RestDevice> devices)
            => JsonSerializer.Serialize(new
            {
                data = devices.Select(d => new
                {
                    type = "device",
                    attributes = new
                    {
                        name = d.Name,
                        state = d.State,
                        volume = d.Volume,
                        ip = d.Ip,
                        port = d.Port,
                        isgroup = d.IsGroup
                    }
                }).ToArray()
            }, Compact);

        /// <summary>An error, in the same envelope the API has always used.</summary>
        public static string Error(string status, string id, string? title = null)
            => JsonSerializer.Serialize(new
            {
                errors = title == null
                    ? (object)new { status, id }
                    : new { status, id, title }
            }, Compact);

        /// <summary>
        /// The whole response, headers and body, ready to send. The length is counted in bytes, because
        /// bytes are what goes out.
        /// </summary>
        public static byte[] Http(bool ok, string body)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var head = Encoding.UTF8.GetBytes(
                (ok ? "HTTP/1.1 200 OK\r\n" : "HTTP/1.1 400 Bad Request\r\n") +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Connection: close\r\n" +
                "\r\n");

            var wire = new byte[head.Length + bodyBytes.Length];
            head.CopyTo(wire, 0);
            bodyBytes.CopyTo(wire, head.Length);
            return wire;
        }
    }
}
