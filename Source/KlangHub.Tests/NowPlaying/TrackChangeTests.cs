using System;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// When the stage is allowed to cut to a new scene. This is the rule the prompt cares most about: the
    /// file name arrives first and the artist follows two seconds later, and that must be the same track
    /// growing more complete - not a second track. Every false positive is a fresh LOAD, and a fresh LOAD
    /// is an audible gap.
    /// </summary>
    public class TrackChangeTests
    {
        [Fact]
        public void The_first_thing_we_learn_is_a_new_track()
        {
            var next = new NowPlayingTrack { Title = "Teardrop" };
            Assert.True(TrackChange.IsNewTrack(null, next));
        }

        [Fact]
        public void Knowing_nothing_at_all_is_not_a_track()
        {
            Assert.False(TrackChange.IsNewTrack(null, new NowPlayingTrack()));
        }

        [Fact]
        public void Learning_the_artist_of_a_known_title_is_the_same_track()
        {
            // The exact case from the prompt: the stage must breathe, not cut.
            var before = new NowPlayingTrack { Title = "Teardrop" };
            var after = new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" };
            Assert.False(TrackChange.IsNewTrack(before, after));
        }

        [Fact]
        public void Learning_the_album_and_the_length_is_still_the_same_track()
        {
            var before = new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" };
            var after = before with { Album = "Mezzanine", Duration = TimeSpan.FromSeconds(330) };
            Assert.False(TrackChange.IsNewTrack(before, after));
        }

        [Fact]
        public void A_different_title_is_a_different_track()
        {
            var before = new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" };
            var after = new NowPlayingTrack { Title = "Angel", Artist = "Massive Attack" };
            Assert.True(TrackChange.IsNewTrack(before, after));
        }

        [Fact]
        public void A_different_artist_under_the_same_title_is_a_different_track()
        {
            var before = new NowPlayingTrack { Title = "Hurt", Artist = "Nine Inch Nails" };
            var after = new NowPlayingTrack { Title = "Hurt", Artist = "Johnny Cash" };
            Assert.True(TrackChange.IsNewTrack(before, after));
        }

        [Theory]
        [InlineData("Teardrop", "  teardrop  ")]
        [InlineData("Teardrop", "TEARDROP")]
        [InlineData("Massive  Attack", "Massive Attack")]
        public void Spelling_that_only_differs_in_case_or_spacing_is_the_same_track(string before, string after)
        {
            // Sources disagree about capitals and stray spaces constantly. None of that is a new song.
            var a = new NowPlayingTrack { Title = before };
            var b = new NowPlayingTrack { Title = after };
            Assert.False(TrackChange.IsNewTrack(a, b));
        }

        [Fact]
        public void A_different_file_is_a_different_track_even_under_the_same_name()
        {
            // Two recordings of the same piece, e.g. a live version after the studio one.
            var before = new NowPlayingTrack { Title = "Teardrop", FilePath = @"D:\Musik\studio\03.flac" };
            var after = new NowPlayingTrack { Title = "Teardrop", FilePath = @"D:\Musik\live\07.flac" };
            Assert.True(TrackChange.IsNewTrack(before, after));
        }

        [Fact]
        public void A_clearly_different_length_is_a_different_track()
        {
            var before = new NowPlayingTrack { Title = "Teardrop", Duration = TimeSpan.FromSeconds(330) };
            var after = new NowPlayingTrack { Title = "Teardrop", Duration = TimeSpan.FromSeconds(512) };
            Assert.True(TrackChange.IsNewTrack(before, after));
        }

        [Fact]
        public void A_length_that_only_wobbles_by_a_second_is_the_same_track()
        {
            // Sources round differently. A second of disagreement is not a new song.
            var before = new NowPlayingTrack { Title = "Teardrop", Duration = TimeSpan.FromSeconds(330) };
            var after = new NowPlayingTrack { Title = "Teardrop", Duration = TimeSpan.FromMilliseconds(330400) };
            Assert.False(TrackChange.IsNewTrack(before, after));
        }

        [Fact]
        public void Forgetting_everything_is_not_a_new_track()
        {
            // A source dropping out must never blank the stage: the last thing we knew stays on screen.
            var before = new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" };
            Assert.False(TrackChange.IsNewTrack(before, new NowPlayingTrack()));
        }
    }
}
