using System;
using System.Linq;
using System.Text;
using KlangHub.Core.Streaming;

namespace KlangHub.Streaming
{
    /// <summary>
    /// Minimal HTTP helpers for the branded now-playing artwork the streaming server serves alongside the audio
    /// stream. The Chromecast fetches this image (referenced from the LOAD metadata) so the Default Media
    /// Receiver shows a full-bleed KlangHub screen instead of the generic placeholder.
    /// </summary>
    public static class ArtworkHttp
    {
        /// <summary>True when the HTTP request targets the artwork path (case-insensitive, query allowed).</summary>
        public static bool IsArtworkRequest(string httpRequest)
        {
            if (string.IsNullOrEmpty(httpRequest))
                return false;

            // First token after the method is the request target, e.g. "GET /artwork.png?v=2 HTTP/1.1".
            var firstLine = httpRequest.Split('\n').FirstOrDefault() ?? string.Empty;
            var parts = firstLine.Split(' ');
            if (parts.Length < 2)
                return false;

            var target = parts[1];
            var path = target;
            var query = string.Empty;
            var split = target.IndexOf('?');
            if (split >= 0)
            {
                path = target.Substring(0, split);
                query = target.Substring(split + 1);
            }

            if (!path.StartsWith("/artwork", StringComparison.OrdinalIgnoreCase))
                return false;

            // And it has to carry this run's secret. The address is handed to the device being cast to;
            // without this, /artwork.png answered anyone on the network who asked - and what it answers
            // with is the cover of whatever is playing, which is a live account of what is being listened
            // to, given to a guest on the wireless for nothing more than asking.
            return SessionSecret.Matches(ValueOf(query, "k"));
        }

        /// <summary>The value of one query parameter, or null when it is not there.</summary>
        private static string? ValueOf(string query, string name)
        {
            foreach (var pair in query.Split('&'))
            {
                var equals = pair.IndexOf('=');
                if (equals < 0)
                    continue;

                if (string.Equals(pair.Substring(0, equals), name, StringComparison.Ordinal))
                    return pair.Substring(equals + 1);
            }

            return null;
        }

        /// <summary>The query string that lets a request through, for whoever builds the address.</summary>
        public static string SecretQuery => "k=" + SessionSecret.Value;

        /// <summary>
        /// What the bytes actually are. Album art found beside a track is usually JPEG, and announcing a
        /// JPEG as image/png is how a receiver ends up showing nothing where a cover should be. Anything
        /// unrecognised is called a PNG, because the rendered fallback always is one.
        /// </summary>
        private static string ContentTypeOf(byte[] image)
        {
            if (image.Length >= 3 && image[0] == 0xFF && image[1] == 0xD8 && image[2] == 0xFF)
                return "image/jpeg";

            if (image.Length >= 3 && image[0] == 0x47 && image[1] == 0x49 && image[2] == 0x46)
                return "image/gif";

            if (image.Length >= 12 && image[0] == 0x52 && image[1] == 0x49 && image[2] == 0x46 && image[3] == 0x46 &&
                image[8] == 0x57 && image[9] == 0x45 && image[10] == 0x42 && image[11] == 0x50)
                return "image/webp";

            return "image/png";
        }

        /// <summary>Build a complete HTTP/1.0 response serving <paramref name="png"/>, closing after.</summary>
        public static byte[] BuildImageResponse(byte[] png)
        {
            png ??= Array.Empty<byte>();

            var header = new StringBuilder();
            header.Append("HTTP/1.0 200 OK\r\n");
            header.Append($"Content-Type: {ContentTypeOf(png)}\r\n");
            header.Append($"Content-Length: {png.Length}\r\n");
            header.Append("Cache-Control: no-cache\r\n");
            // The stage on the television takes its colour from this picture, which means drawing it
            // into a canvas and reading the pixels back. Without this the browser marks that canvas
            // tainted and the read throws - the screen would fall back to a flat grey with nothing to
            // explain why. What keeps this from being an open door is the secret in the address: the
            // header says any page may read the answer, not that anyone may ask the question.
            header.Append("Access-Control-Allow-Origin: *\r\n");
            header.Append("Connection: close\r\n");
            header.Append("\r\n");

            var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
            var response = new byte[headerBytes.Length + png.Length];
            Buffer.BlockCopy(headerBytes, 0, response, 0, headerBytes.Length);
            Buffer.BlockCopy(png, 0, response, headerBytes.Length, png.Length);
            return response;
        }
    }
}
