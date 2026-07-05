using System.Text;
using KlangHub.Streaming;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ArtworkHttpTests
    {
        [Theory]
        [InlineData("GET /artwork.png HTTP/1.1\r\nHost: x\r\n\r\n", true)]
        [InlineData("GET /artwork.png?v=2 HTTP/1.1\r\n\r\n", true)]
        [InlineData("GET /ARTWORK.PNG HTTP/1.1\r\n\r\n", true)]
        [InlineData("GET / HTTP/1.1\r\nHost: x\r\n\r\n", false)]
        [InlineData("GET /stream.wav HTTP/1.1\r\n\r\n", false)]
        [InlineData("", false)]
        public void IsArtworkRequest_matches_only_the_artwork_path(string request, bool expected)
        {
            Assert.Equal(expected, ArtworkHttp.IsArtworkRequest(request));
        }

        [Fact]
        public void BuildImageResponse_prefixes_a_png_http_header_then_the_bytes()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 };

            var response = ArtworkHttp.BuildImageResponse(png);

            var headerEnd = System.Array.IndexOf(response, (byte)'\r');
            var text = Encoding.ASCII.GetString(response);
            Assert.StartsWith("HTTP/1.0 200 OK\r\n", text);
            Assert.Contains("Content-Type: image/png\r\n", text);
            Assert.Contains("Content-Length: 8\r\n", text);
            Assert.Contains("Connection: close\r\n", text);

            // The raw PNG bytes are appended verbatim after the header block.
            var separator = Encoding.ASCII.GetBytes("\r\n\r\n");
            var idx = IndexOf(response, separator);
            Assert.True(idx >= 0, "expected header/body separator");
            var body = response[(idx + separator.Length)..];
            Assert.Equal(png, body);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }
    }
}
