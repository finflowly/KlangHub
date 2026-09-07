using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class StageCoverTests
    {
        private const string Artwork = "http://192.168.1.5:8080/artwork.png?k=s&lang=de&cover=abc123";

        [Fact]
        public void A_track_that_carries_its_own_picture_is_served_from_this_machine()
        {
            Assert.Equal(Artwork, StageCover.Url(Artwork, "abc123", onlineUrl: "https://coverartarchive.org/release/1/front-500"));
        }

        [Fact]
        public void A_track_without_a_picture_of_its_own_may_borrow_one_from_the_web()
        {
            Assert.Equal("https://coverartarchive.org/release/1/front-500",
                StageCover.Url(Artwork, string.Empty, "https://coverartarchive.org/release/1/front-500"));
        }

        [Fact]
        public void A_track_nobody_has_a_picture_for_shows_the_rings()
        {
            Assert.Null(StageCover.Url(Artwork, string.Empty, null));
            Assert.Null(StageCover.Url(Artwork, string.Empty, "   "));
        }

        [Fact]
        public void Nothing_is_offered_while_this_machine_has_no_address_to_be_reached_at()
        {
            Assert.Null(StageCover.Url(string.Empty, "abc123", null));
        }

        [Fact]
        public void A_borrowed_picture_is_only_taken_from_the_open_web()
        {
            Assert.Null(StageCover.Url(Artwork, string.Empty, "file:///C:/cover.jpg"));
            Assert.Null(StageCover.Url(Artwork, string.Empty, "javascript:alert(1)"));
        }
    }
}
