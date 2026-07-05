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

        /// <summary>Build a complete HTTP/1.0 response serving <paramref name="png"/> as image/png, closing after.</summary>
        public static byte[] BuildImageResponse(byte[] png)
        {
            png ??= Array.Empty<byte>();

            var header = new StringBuilder();
            header.Append("HTTP/1.0 200 OK\r\n");
            header.Append("Content-Type: image/png\r\n");
            header.Append($"Content-Length: {png.Length}\r\n");
            header.Append("Cache-Control: no-cache\r\n");
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
