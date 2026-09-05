using System.Text;
using KlangHub.Core.Streaming;
using KlangHub.Streaming;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ArtworkHttpTests
    {
        /// <summary>The address the device is actually given, with this run's secret in it.</summary>
        private static string Ours(string target)
            => "GET " + target.Replace("{k}", ArtworkHttp.SecretQuery) + " HTTP/1.1\r\nHost: x\r\n\r\n";

        [Theory]
        [InlineData("/artwork.png?{k}", true)]
        [InlineData("/artwork.png?{k}&lang=de", true)]
        [InlineData("/artwork.png?lang=de&{k}", true)]
        [InlineData("/ARTWORK.PNG?{k}", true)]
        [InlineData("/", false)]
        [InlineData("/stream.wav?{k}", false)]
        public void IsArtworkRequest_matches_only_the_artwork_path(string target, bool expected)
        {
            Assert.Equal(expected, ArtworkHttp.IsArtworkRequest(Ours(target)));
        }

        [Theory]
        [InlineData("GET /artwork.png HTTP/1.1\r\nHost: x\r\n\r\n")]
        [InlineData("GET /artwork.png?v=2 HTTP/1.1\r\n\r\n")]
        [InlineData("GET /artwork.png?k= HTTP/1.1\r\n\r\n")]
        [InlineData("GET /artwork.png?k=guessing HTTP/1.1\r\n\r\n")]
        [InlineData("GET /artwork.png?kk=x HTTP/1.1\r\n\r\n")]
        [InlineData("")]
        public void The_cover_is_not_handed_to_whoever_asks(string request)
        {
            // The cover of what is playing right now is a live answer to "what are they listening to",
            // and it used to be given to anyone on the network for the asking - a guest on the wireless,
            // or anything else that had found its way on. The address goes to the device being cast to,
            // and the secret in it is what keeps it there.
            Assert.False(ArtworkHttp.IsArtworkRequest(request));
        }

        [Fact]
        public void A_secret_of_the_right_length_but_the_wrong_value_is_still_wrong()
        {
            var wrong = new string('A', SessionSecret.Value.Length);

            Assert.False(SessionSecret.Matches(wrong));
            Assert.False(SessionSecret.Matches(null));
            Assert.False(SessionSecret.Matches(""));
            Assert.True(SessionSecret.Matches(SessionSecret.Value));
        }

        [Fact]
        public void The_secret_is_long_enough_to_be_worth_having()
        {
            // 128 bits as base64url: 22 characters. Short enough to sit in a URL, long enough that
            // guessing it is not a strategy.
            Assert.Equal(22, SessionSecret.Value.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", SessionSecret.Value);
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

        [Fact]
        public void A_jpeg_cover_is_served_as_a_jpeg()
        {
            // Album art off a disc is usually JPEG. Announcing it as image/png is how a receiver ends up
            // showing nothing at all where a cover should be.
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };

            var response = System.Text.Encoding.ASCII.GetString(ArtworkHttp.BuildImageResponse(jpeg));

            Assert.Contains("Content-Type: image/jpeg", response);
        }

        [Fact]
        public void The_rendered_artwork_is_still_served_as_a_png()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

            var response = System.Text.Encoding.ASCII.GetString(ArtworkHttp.BuildImageResponse(png));

            Assert.Contains("Content-Type: image/png", response);
        }

        [Fact]
        public void Anything_unrecognised_is_still_called_a_png()
        {
            // The rendered fallback is always a PNG, so that is the safe thing to claim when in doubt.
            var response = System.Text.Encoding.ASCII.GetString(ArtworkHttp.BuildImageResponse(new byte[] { 1, 2, 3 }));

            Assert.Contains("Content-Type: image/png", response);
        }


        [Fact]
        public void The_artwork_may_be_read_pixel_by_pixel_by_the_receiver()
        {
            // The stage takes its colour from the cover, which means drawing the image into a canvas and
            // reading it back. Without this header the browser marks that canvas tainted and the read
            // throws - the television would fall back to a flat grey and the whole "cover gives the
            // colour" idea would quietly stop working, with nothing on screen to say why.
            var response = System.Text.Encoding.ASCII.GetString(
                ArtworkHttp.BuildImageResponse(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 }));

            Assert.Contains("Access-Control-Allow-Origin: *", response);
        }

    }
}
