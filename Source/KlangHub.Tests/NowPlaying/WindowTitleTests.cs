using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// Reading what a player writes in its own title bar.
    /// <para>
    /// Measured on 2026-09-05: Clementine, playing, reports NOTHING to Windows' now-playing session -
    /// GetSessions() returns zero. Its window title at that same moment read "Aquanote - Nowhere (Speakeasy
    /// remix)". For a listener casting from Clementine this is the difference between setting up a helper
    /// script and it simply working, so it is worth reading carefully - and carefully means refusing to
    /// guess, because a title bar also carries version numbers, programme names and idle states.
    /// </para>
    /// </summary>
    public class WindowTitleTests
    {
        [Fact]
        public void Reads_the_artist_and_title_a_player_puts_in_its_title_bar()
        {
            // The real measurement, verbatim.
            var track = PlayerWindowTitle.Parse("clementine", "Aquanote - Nowhere (Speakeasy remix)");

            Assert.Equal("Aquanote", track.Artist);
            Assert.Equal("Nowhere (Speakeasy remix)", track.Title);
        }

        [Theory]
        [InlineData("clementine", "Clementine")]
        [InlineData("clementine", "Clementine 1.4.0rc1")]
        [InlineData("foobar2000", "foobar2000 v2.1")]
        [InlineData("vlc", "VLC media player")]
        [InlineData("aimp", "AIMP")]
        public void A_player_sitting_idle_says_nothing(string process, string title)
        {
            // Nothing is playing, or nothing is known. Putting "Clementine 1.4.0rc1" on a television as if
            // it were a song is exactly the cheap screen this whole feature exists to prevent.
            Assert.True(PlayerWindowTitle.Parse(process, title).IsEmpty);
        }

        [Theory]
        [InlineData("Aquanote - Nowhere - Clementine", "Aquanote", "Nowhere")]
        [InlineData("Massive Attack - Teardrop - VLC media player", "Massive Attack", "Teardrop")]
        [InlineData("Xtal - Aphex Twin - foobar2000 v2.1", "Xtal", "Aphex Twin")]
        public void Strips_the_programme_name_players_append(string title, string artist, string song)
        {
            // Several players hang their own name on the end. It is not part of the music.
            var track = PlayerWindowTitle.Parse("any", title);

            Assert.Equal(artist, track.Artist);
            Assert.Equal(song, track.Title);
        }

        [Fact]
        public void Keeps_a_dash_that_belongs_to_the_song()
        {
            var track = PlayerWindowTitle.Parse("clementine", "U2 - Sunday Bloody Sunday - Live");

            Assert.Equal("U2", track.Artist);
            Assert.Equal("Sunday Bloody Sunday - Live", track.Title);
        }

        [Fact]
        public void A_lone_title_stays_a_title()
        {
            var track = PlayerWindowTitle.Parse("clementine", "Teardrop");

            Assert.Equal("Teardrop", track.Title);
            Assert.Null(track.Artist);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Nothing_in_the_title_bar_means_nothing_known(string? title)
        {
            Assert.True(PlayerWindowTitle.Parse("clementine", title).IsEmpty);
        }

        [Fact]
        public void A_paused_marker_is_not_part_of_the_song()
        {
            // Some players prefix the state. The stage has its own way of showing that.
            var track = PlayerWindowTitle.Parse("clementine", "[Paused] Aquanote - Nowhere");

            Assert.Equal("Aquanote", track.Artist);
            Assert.Equal("Nowhere", track.Title);
        }

        [Fact]
        public void Refuses_a_title_that_is_only_the_programme_with_a_dash()
        {
            // "Clementine - " and friends: a shape that parses cleanly but means nothing.
            Assert.True(PlayerWindowTitle.Parse("clementine", "Clementine - ").IsEmpty);
        }
    }
}
