using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// Reading the now-playing text file a player or a little helper keeps up to date. There is no standard
    /// for this file - every player and every helper writes it differently - so the parser has to recognise
    /// the handful of shapes people actually use, and say "I don't know" rather than invent something for
    /// the rest.
    /// </summary>
    public class NowPlayingTextTests
    {
        [Fact]
        public void Reads_the_common_single_line()
        {
            var track = NowPlayingText.Parse("Massive Attack - Teardrop");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
        }

        [Fact]
        public void A_lone_line_without_a_separator_is_a_title()
        {
            // Claiming an artist here would put a guess on a four-metre screen.
            var track = NowPlayingText.Parse("Teardrop");

            Assert.Equal("Teardrop", track.Title);
            Assert.Null(track.Artist);
        }

        [Fact]
        public void Splits_a_single_line_at_the_first_separator_only()
        {
            // Titles contain dashes. "Sunday Bloody Sunday - Live" must not lose its second half.
            var track = NowPlayingText.Parse("U2 - Sunday Bloody Sunday - Live");

            Assert.Equal("U2", track.Artist);
            Assert.Equal("Sunday Bloody Sunday - Live", track.Title);
        }

        [Fact]
        public void A_hyphen_without_spaces_is_part_of_the_name()
        {
            // "Jay-Z" is one word, not an artist called "Jay" playing "Z".
            var track = NowPlayingText.Parse("Jay-Z");

            Assert.Equal("Jay-Z", track.Title);
            Assert.Null(track.Artist);
        }

        [Fact]
        public void Reads_three_plain_lines_as_artist_title_album()
        {
            var track = NowPlayingText.Parse("Massive Attack\nTeardrop\nMezzanine");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
            Assert.Equal("Mezzanine", track.Album);
        }

        [Theory]
        [InlineData("artist: Massive Attack\ntitle: Teardrop\nalbum: Mezzanine")]
        [InlineData("ARTIST=Massive Attack\nTITLE=Teardrop\nALBUM=Mezzanine")]
        [InlineData("$artist: Massive Attack\n$title: Teardrop\n$album: Mezzanine")]
        public void Reads_labelled_lines_however_they_are_written(string content)
        {
            // Helpers write "artist:", "ARTIST=" and VLC-style "$artist" - all of them mean the same thing.
            var track = NowPlayingText.Parse(content);

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
            Assert.Equal("Mezzanine", track.Album);
        }

        [Fact]
        public void Labelled_lines_may_arrive_in_any_order_and_incomplete()
        {
            var track = NowPlayingText.Parse("title: Teardrop\nfile: D:\\Musik\\03.flac");

            Assert.Equal("Teardrop", track.Title);
            Assert.Equal("D:\\Musik\\03.flac", track.FilePath);
            Assert.Null(track.Artist);
        }

        [Fact]
        public void A_file_path_is_worth_having_on_its_own()
        {
            // The path is the key to the tags and the cover - the richest thing this file can carry.
            var track = NowPlayingText.Parse("file=D:\\Musik\\Massive Attack - Mezzanine\\03 Teardrop.flac");

            Assert.Equal("D:\\Musik\\Massive Attack - Mezzanine\\03 Teardrop.flac", track.FilePath);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\n\n\n")]
        public void Nothing_written_means_nothing_known(string content)
        {
            Assert.True(NowPlayingText.Parse(content).IsEmpty);
        }

        [Fact]
        public void An_unfilled_template_is_not_a_track()
        {
            // A helper that fired before the player told it anything leaves the placeholders standing.
            // Putting "$artist" on the television would be worse than showing nothing.
            Assert.True(NowPlayingText.Parse("$artist - $title").IsEmpty);
        }

        [Fact]
        public void Ignores_blank_lines_around_the_content()
        {
            var track = NowPlayingText.Parse("\r\n Massive Attack \r\n Teardrop \r\n\r\n");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
        }

        [Fact]
        public void Two_plain_lines_are_artist_and_title()
        {
            var track = NowPlayingText.Parse("Massive Attack\nTeardrop");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
            Assert.Null(track.Album);
        }
    }
}
