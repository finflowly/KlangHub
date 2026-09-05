using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// The last resort: what a path alone can tell us. Everything here is a guess and is marked as one by
    /// the source rank it arrives with - the rule is to guess only where the shape is unmistakable, and to
    /// say nothing rather than put an invented artist on a television.
    /// </summary>
    public class FileNameGuessTests
    {
        [Fact]
        public void Reads_artist_and_title_from_the_usual_shape()
        {
            var track = FileNameGuess.Parse(@"D:\Musik\Massive Attack - Teardrop.mp3");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
        }

        [Theory]
        [InlineData(@"D:\Musik\03 Teardrop.flac")]
        [InlineData(@"D:\Musik\03 - Teardrop.flac")]
        [InlineData(@"D:\Musik\03. Teardrop.flac")]
        [InlineData(@"D:\Musik\03_Teardrop.flac")]
        public void Strips_a_leading_track_number(string path)
        {
            Assert.Equal("Teardrop", FileNameGuess.Parse(path).Title);
        }

        [Fact]
        public void Reads_artist_and_album_from_the_folder()
        {
            var track = FileNameGuess.Parse(@"D:\Musik\Massive Attack - Mezzanine\03 Teardrop.flac");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Mezzanine", track.Album);
            Assert.Equal("Teardrop", track.Title);
        }

        [Fact]
        public void The_file_itself_wins_over_the_folder_for_the_artist()
        {
            // A compilation folder says one thing, the file another. The file is closer to the track.
            var track = FileNameGuess.Parse(@"D:\Musik\Various - Sampler\Aphex Twin - Xtal.flac");

            Assert.Equal("Aphex Twin", track.Artist);
            Assert.Equal("Xtal", track.Title);
        }

        [Fact]
        public void A_folder_that_is_not_shaped_like_artist_and_album_says_nothing()
        {
            var track = FileNameGuess.Parse(@"D:\Musik\Neuer Ordner\03 Teardrop.flac");

            Assert.Equal("Teardrop", track.Title);
            Assert.Null(track.Artist);
            Assert.Null(track.Album);
        }

        [Fact]
        public void Always_carries_the_path_itself()
        {
            // Even when nothing can be read from it: the path is what lets the tag reader take over.
            var track = FileNameGuess.Parse(@"D:\Musik\x.flac");

            Assert.Equal(@"D:\Musik\x.flac", track.FilePath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Nothing_in_means_nothing_out(string? path)
        {
            Assert.True(FileNameGuess.Parse(path).IsEmpty);
        }

        [Fact]
        public void A_bare_number_is_not_a_title()
        {
            // "03.flac" tells us nothing worth showing; the path still goes through for the tag reader.
            var track = FileNameGuess.Parse(@"D:\Musik\03.flac");

            Assert.Null(track.Title);
            Assert.Equal(@"D:\Musik\03.flac", track.FilePath);
        }

        [Fact]
        public void Underscores_are_read_as_spaces()
        {
            var track = FileNameGuess.Parse(@"D:\Musik\Massive_Attack_-_Teardrop.mp3");

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
        }
    }
}
