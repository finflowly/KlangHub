using System;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// Turning what Windows' now-playing session reports into something worth showing. Players fill that
    /// session in carelessly: some put "Artist - Title" in the title field, some put the file name there,
    /// some leave the artist blank and fill in the album artist instead. This is where that is straightened
    /// out - and where the temptation to invent is resisted.
    /// </summary>
    public class SmtcReadingTests
    {
        [Fact]
        public void Passes_through_a_session_that_was_filled_in_properly()
        {
            var track = new SmtcReading
            {
                Title = "Teardrop",
                Artist = "Massive Attack",
                AlbumTitle = "Mezzanine",
                Duration = TimeSpan.FromSeconds(330)
            }.ToTrack();

            Assert.Equal("Teardrop", track.Title);
            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Mezzanine", track.Album);
            Assert.Equal(TimeSpan.FromSeconds(330), track.Duration);
        }

        [Fact]
        public void Splits_artist_and_title_when_a_player_crammed_both_into_the_title()
        {
            // Common in browser players and in a few desktop ones. Only done when the artist field is
            // empty - if the player told us an artist, its title stays whole.
            var track = new SmtcReading { Title = "Massive Attack - Teardrop" }.ToTrack();

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
        }

        [Fact]
        public void Leaves_a_title_alone_when_the_artist_is_known()
        {
            // "Sunday Bloody Sunday - Live" is a title, not an artist and a title.
            var track = new SmtcReading { Title = "Sunday Bloody Sunday - Live", Artist = "U2" }.ToTrack();

            Assert.Equal("Sunday Bloody Sunday - Live", track.Title);
            Assert.Equal("U2", track.Artist);
        }

        [Fact]
        public void Reads_a_file_name_that_was_dropped_into_the_title_field()
        {
            // Players that know nothing about the file still put its name somewhere. Showing
            // "03 Teardrop.flac" raw is exactly the screen the stage exists to avoid.
            var track = new SmtcReading { Title = "03 Teardrop.flac" }.ToTrack();

            Assert.Equal("Teardrop", track.Title);
        }

        [Fact]
        public void Falls_back_to_the_album_artist()
        {
            var track = new SmtcReading { Title = "Teardrop", AlbumArtist = "Massive Attack" }.ToTrack();

            Assert.Equal("Massive Attack", track.Artist);
        }

        [Fact]
        public void Prefers_the_track_artist_over_the_album_artist()
        {
            var track = new SmtcReading { Title = "Xtal", Artist = "Aphex Twin", AlbumArtist = "Various Artists" }.ToTrack();

            Assert.Equal("Aphex Twin", track.Artist);
        }

        [Fact]
        public void A_length_of_zero_is_not_a_length()
        {
            // A live stream reports zero. Passing that on would draw a full progress line under a piece
            // that has no end.
            var track = new SmtcReading { Title = "Teardrop", Duration = TimeSpan.Zero }.ToTrack();

            Assert.Null(track.Duration);
        }

        [Fact]
        public void An_empty_session_says_nothing()
        {
            Assert.True(new SmtcReading().ToTrack().IsEmpty);
        }

        [Fact]
        public void Trims_what_the_player_left_lying_around()
        {
            var track = new SmtcReading { Title = "  Teardrop  ", Artist = "  Massive Attack " }.ToTrack();

            Assert.Equal("Teardrop", track.Title);
            Assert.Equal("Massive Attack", track.Artist);
        }
    }
}
