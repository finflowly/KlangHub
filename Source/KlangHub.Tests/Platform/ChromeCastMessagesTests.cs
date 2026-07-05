using KlangHub.Communication;
using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ChromeCastMessagesTests
    {
        private static CastMediaMetadata Meta(string contentType = "audio/flac") => new CastMediaMetadata
        {
            Title = "Wohnzimmer",
            Subtitle = "Live vom PC",
            Album = "KlangHub",
            ImageUrl = "http://192.168.1.5:8080/artwork.png",
            ContentType = contentType
        };

        [Theory]
        [InlineData("audio/wav")]
        [InlineData("audio/mpeg")]
        [InlineData("audio/flac")]
        public void GetLoadMessage_uses_the_supplied_content_type(string contentType)
        {
            var msg = new ChromeCastMessages().GetLoadMessage("http://192.168.1.5:8080/", "client-1", "web-1", 42, Meta(contentType));

            Assert.Contains($"\"contentType\":\"{contentType}\"", msg.PayloadUtf8);
        }

        [Fact]
        public void GetLoadMessage_carries_music_track_metadata()
        {
            var msg = new ChromeCastMessages().GetLoadMessage("http://192.168.1.5:8080/", "client-1", "web-1", 7, Meta());

            var payload = msg.PayloadUtf8;
            Assert.Contains("\"metadataType\":3", payload);   // MusicTrackMediaMetadata
            Assert.Contains("\"title\":\"Wohnzimmer\"", payload);
            Assert.Contains("\"artist\":\"Live vom PC\"", payload);
            Assert.Contains("\"albumName\":\"KlangHub\"", payload);
        }

        [Fact]
        public void GetLoadMessage_includes_the_artwork_image_url()
        {
            var msg = new ChromeCastMessages().GetLoadMessage("http://192.168.1.5:8080/", "client-1", "web-1", 7, Meta());

            var payload = msg.PayloadUtf8;
            Assert.Contains("\"url\":\"http://192.168.1.5:8080/artwork.png\"", payload);
        }
    }
}
