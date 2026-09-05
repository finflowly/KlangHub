using System;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// What the television is told over the live channel. Two rules run through all of it: never send a
    /// field we do not know (the stage keeps what it had rather than blanking), and never state a technical
    /// fact we cannot back up - an invented "FLAC · 24 Bit" on a quality mark is worse than no mark at all.
    /// </summary>
    public class StageUpdateTests
    {
        [Fact]
        public void Carries_what_is_known_about_the_piece()
        {
            var update = StageUpdate.For(
                new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack", Album = "Mezzanine" },
                zone: "Wohnzimmer", coverUrl: "http://host/artwork.png", isNewTrack: true);

            Assert.Equal("Teardrop", update.Title);
            Assert.Equal("Massive Attack", update.Artist);
            Assert.Equal("Mezzanine", update.Album);
            Assert.Equal("Wohnzimmer", update.Zone);
            Assert.Equal("http://host/artwork.png", update.Cover);
            Assert.True(update.NewTrack);
        }

        [Fact]
        public void Says_nothing_about_fields_it_does_not_know()
        {
            // Null, not "". An empty string would be an instruction to clear a line the stage may already
            // be showing correctly from an earlier, better source.
            var update = StageUpdate.For(new NowPlayingTrack { Title = "Teardrop" }, null, null, false);

            Assert.Null(update.Artist);
            Assert.Null(update.Album);
            Assert.Null(update.Zone);
            Assert.Null(update.Cover);
        }

        [Fact]
        public void Builds_the_quality_mark_from_what_the_file_actually_said()
        {
            var update = StageUpdate.For(
                new NowPlayingTrack { Title = "Teardrop", Format = "FLAC", BitDepth = 24, SampleRate = 96000 },
                null, null, false);

            Assert.Equal("FLAC · 24 Bit · 96 kHz", update.Quality);
        }

        [Fact]
        public void Leaves_out_the_parts_of_the_quality_mark_it_cannot_prove()
        {
            var update = StageUpdate.For(
                new NowPlayingTrack { Title = "Teardrop", Format = "MP3" }, null, null, false);

            Assert.Equal("MP3", update.Quality);
        }

        [Fact]
        public void No_quality_mark_at_all_when_nothing_is_known()
        {
            var update = StageUpdate.For(new NowPlayingTrack { Title = "Teardrop" }, null, null, false);

            Assert.Null(update.Quality);
        }

        [Theory]
        [InlineData(44100, "44,1 kHz")]
        [InlineData(48000, "48 kHz")]
        [InlineData(96000, "96 kHz")]
        [InlineData(192000, "192 kHz")]
        public void Writes_sample_rates_the_way_a_listener_reads_them(int hertz, string expected)
        {
            // "44100 Hz" is a number out of a datasheet; "44,1 kHz" is what is printed on the sleeve.
            var update = StageUpdate.For(
                new NowPlayingTrack { Title = "Teardrop", SampleRate = hertz }, null, null, false);

            Assert.Equal(expected, update.Quality);
        }

        [Fact]
        public void The_length_travels_only_when_it_is_real()
        {
            var known = StageUpdate.For(
                new NowPlayingTrack { Title = "Teardrop", Duration = TimeSpan.FromSeconds(330) }, null, null, false);
            var unknown = StageUpdate.For(new NowPlayingTrack { Title = "Teardrop" }, null, null, false);

            Assert.Equal(330, known.Duration);
            // A loopback stream has no end. Sending zero would draw a finished progress line under it.
            Assert.Null(unknown.Duration);
        }

        [Fact]
        public void An_empty_track_is_not_worth_sending()
        {
            Assert.Null(StageUpdate.For(new NowPlayingTrack(), "Wohnzimmer", null, false));
        }
    }
}
