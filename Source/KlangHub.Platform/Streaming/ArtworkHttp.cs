using System;
using System.Linq;
using System.Text;

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

            var path = parts[1];
            var query = path.IndexOf('?');
            if (query >= 0)
                path = path.Substring(0, query);

            return path.StartsWith("/artwork", StringComparison.OrdinalIgnoreCase);
        }

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
            // explain why. The image is already served to anyone on the network who asks for the audio.
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
