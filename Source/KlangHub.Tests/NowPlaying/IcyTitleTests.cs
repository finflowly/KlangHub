using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class IcyTitleTests
    {
        [Theory]
        [InlineData("Massive Attack - Teardrop", "Massive Attack", "Teardrop")]
        [InlineData("  Portishead  -  Glory Box  ", "Portishead", "Glory Box")]
        [InlineData("Jean-Michel Jarre - Oxygene", "Jean-Michel Jarre", "Oxygene")]
        [InlineData("Simon - Ballad Of A Well-Known Gun", "Simon", "Ballad Of A Well-Known Gun")]
        [InlineData("AC/DC - T.N.T.", "AC/DC", "T.N.T.")]
        public void One_line_that_names_both_is_taken_apart(string line, string artist, string title)
        {
            var split = IcyTitle.Split(line);

            Assert.True(split.Certain);
            Assert.Equal(artist, split.Artist);
            Assert.Equal(title, split.Title);
        }

        [Theory]
        [InlineData("Radio Paradise")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Ballad Of A Well-Known Gun")]
        [InlineData("A - B - C")]
        [InlineData(" - Teardrop")]
        [InlineData("Massive Attack - ")]
        public void A_line_that_could_mean_two_things_is_left_alone(string line)
        {
            var split = IcyTitle.Split(line);

            Assert.False(split.Certain);
            Assert.Null(split.Artist);
        }

        [Fact]
        public void A_hyphen_without_room_around_it_belongs_to_the_song()
        {
            Assert.False(IcyTitle.Split("Jean-Michel Jarre").Certain);
            Assert.False(IcyTitle.Split("Sunday-Morning").Certain);
        }

        [Fact]
        public void What_is_left_alone_keeps_the_line_as_the_title()
        {
            var split = IcyTitle.Split("Radio Paradise");

            Assert.Equal("Radio Paradise", split.Title);
        }
    }
}
