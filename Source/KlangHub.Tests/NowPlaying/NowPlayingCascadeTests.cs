using System;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// The five merge rules from the spec. Together they are what stands between a listener and the
    /// "Unknown / empty cover / raw file name" screen the whole stage exists to avoid.
    /// </summary>
    public class NowPlayingCascadeTests
    {
        [Fact]
        public void Knows_nothing_until_something_is_contributed()
        {
            Assert.True(new NowPlayingCascade().Current.IsEmpty);
        }

        [Fact]
        public void Fields_are_merged_one_by_one_not_track_by_track()
        {
            // Rule 1. The artist comes from the now-playing file, the album from the tags, and the stage
            // gets both - neither source has to be complete on its own.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.NowPlayingFile, new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" });
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Album = "Mezzanine" });

            Assert.Equal("Massive Attack", cascade.Current.Artist);
            Assert.Equal("Mezzanine", cascade.Current.Album);
        }

        [Fact]
        public void A_better_source_replaces_a_weaker_one()
        {
            // Rule 2. The file name guessed "03 Teardrop"; the tags know better.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileName, new NowPlayingTrack { Title = "03 Teardrop" });
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop" });

            Assert.Equal("Teardrop", cascade.Current.Title);
        }

        [Fact]
        public void A_weaker_source_never_undoes_a_better_one()
        {
            // Rule 2, the other way round - and the order sources answer in is not ours to control.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop" });
            cascade.Contribute(MetadataSource.FileName, new NowPlayingTrack { Title = "03 Teardrop" });

            Assert.Equal("Teardrop", cascade.Current.Title);
        }

        [Fact]
        public void An_empty_value_never_overwrites_a_known_one()
        {
            // Rule 3. This is what stops placeholders from flickering on a four-metre screen.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.NowPlayingFile, new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" });
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Artist = "   " });

            Assert.Equal("Massive Attack", cascade.Current.Artist);
        }

        [Fact]
        public void A_source_may_correct_itself()
        {
            // Rule 4. Without this a player could never fix its own mistake.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.SystemMediaControls, new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Atack" });
            cascade.Contribute(MetadataSource.SystemMediaControls, new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" });

            Assert.Equal("Massive Attack", cascade.Current.Artist);
        }

        [Fact]
        public void A_new_track_clears_what_was_known_about_the_old_one()
        {
            // Rule 5. Otherwise the previous album hangs off the new piece.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Album = "Mezzanine", Artist = "Massive Attack" });
            cascade.Contribute(MetadataSource.SystemMediaControls, new NowPlayingTrack { Title = "Xtal", Artist = "Aphex Twin" });

            Assert.Equal("Xtal", cascade.Current.Title);
            Assert.Equal("Aphex Twin", cascade.Current.Artist);
            Assert.Null(cascade.Current.Album);
        }

        [Fact]
        public void Enrichment_keeps_everything_that_was_already_known()
        {
            // The counterpart to rule 5: this must NOT clear, or the stage loses the album it just learned.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Album = "Mezzanine" });
            cascade.Contribute(MetadataSource.SystemMediaControls, new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" });

            Assert.Equal("Mezzanine", cascade.Current.Album);
            Assert.Equal("Massive Attack", cascade.Current.Artist);
        }

        [Fact]
        public void Announces_a_change_worth_showing()
        {
            var cascade = new NowPlayingCascade();
            NowPlayingTrack? announced = null;
            cascade.Changed += (_, track) => announced = track;

            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop" });

            Assert.Equal("Teardrop", announced?.Title);
        }

        [Fact]
        public void Stays_quiet_when_nothing_actually_changed()
        {
            // The sources poll. Re-announcing an unchanged track would reload the receiver over and over.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop" });

            var announcements = 0;
            cascade.Changed += (_, _) => announcements++;
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop" });

            Assert.Equal(0, announcements);
        }

        [Fact]
        public void Reports_whether_the_scene_may_cut()
        {
            // The consumer needs to tell "same track, now with an artist" from "different piece" - one is a
            // soft update, the other is a fresh LOAD.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileName, new NowPlayingTrack { Title = "Teardrop" });

            Assert.False(cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack" }).IsNewTrack);
            Assert.True(cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Angel" }).IsNewTrack);
        }

        [Fact]
        public void A_guessed_name_may_not_claim_a_track_change()
        {
            // Without this rule a tidy title read from the tags is thrown away and replaced by the raw
            // "03 Teardrop" off the disc - the exact screen the stage exists to avoid.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Album = "Mezzanine" });

            var result = cascade.Contribute(MetadataSource.FileName, new NowPlayingTrack { Title = "03 Teardrop" });

            Assert.False(result.IsNewTrack);
            Assert.Equal("Mezzanine", cascade.Current.Album);
        }

        [Fact]
        public void A_guessed_name_from_a_different_file_is_still_a_track_change()
        {
            // The one hard fact a guess carries: another file is another recording.
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack { Title = "Teardrop", Album = "Mezzanine", FilePath = @"D:\Musik\03.flac" });

            var result = cascade.Contribute(MetadataSource.FileName, new NowPlayingTrack { Title = "Angel", FilePath = @"D:\Musik\04.flac" });

            Assert.True(result.IsNewTrack);
            Assert.Null(cascade.Current.Album);
        }

        [Fact]
        public void Carries_the_technical_facts_that_earn_the_quality_mark()
        {
            var cascade = new NowPlayingCascade();
            cascade.Contribute(MetadataSource.FileTags, new NowPlayingTrack
            {
                Title = "Teardrop",
                Format = "FLAC",
                SampleRate = 44100,
                BitDepth = 16,
                Duration = TimeSpan.FromSeconds(330)
            });

            Assert.Equal("FLAC", cascade.Current.Format);
            Assert.Equal(44100, cascade.Current.SampleRate);
            Assert.Equal(16, cascade.Current.BitDepth);
            Assert.Equal(TimeSpan.FromSeconds(330), cascade.Current.Duration);
        }
    }
}
