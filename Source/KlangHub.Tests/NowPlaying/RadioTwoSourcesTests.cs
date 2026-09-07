using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class RadioTwoSourcesTests
    {
        private const string OneLine = "Depeche Mode - Enjoy The Silence - Radio Edit";

        private static NowPlayingTrack FromClementine(string line)
        {
            var split = IcyTitle.Split(line);
            return new NowPlayingTrack { Artist = split.Artist, Title = split.Title };
        }

        private static NowPlayingTrack FromTheWindowTitle(string line) =>
            PlayerWindowTitle.Parse("clementine", line + " - Clementine");

        [Fact]
        public void The_two_ways_of_reading_one_icy_line_do_not_agree()
        {
            var clementine = FromClementine(OneLine);
            var window = FromTheWindowTitle(OneLine);

            Assert.Null(clementine.Artist);
            Assert.Equal(OneLine, clementine.Title);

            Assert.Equal("Depeche Mode", window.Artist);
            Assert.Equal("Enjoy The Silence - Radio Edit", window.Title);
        }

        [Fact]
        public void A_second_reading_of_the_same_line_is_not_a_second_song()
        {
            var cascade = new NowPlayingCascade();

            var first = cascade.Contribute(MetadataSource.PlayerRemote, FromClementine(OneLine));
            var second = cascade.Contribute(MetadataSource.WindowTitle, FromTheWindowTitle(OneLine));

            Assert.True(first.IsNewTrack);
            Assert.False(second.IsNewTrack,
                "the same icy line, read a second way, was taken for a new song - " +
                "and a new song throws the cover that was just found away");
        }

        [Fact]
        public void The_line_does_not_flip_back_and_forth_while_the_song_keeps_playing()
        {
            var cascade = new NowPlayingCascade();
            var changes = 0;

            foreach (var source in new[]
                     {
                         MetadataSource.PlayerRemote, MetadataSource.WindowTitle,
                         MetadataSource.PlayerRemote, MetadataSource.WindowTitle,
                         MetadataSource.PlayerRemote,
                     })
            {
                var track = source == MetadataSource.PlayerRemote
                    ? FromClementine(OneLine)
                    : FromTheWindowTitle(OneLine);

                if (cascade.Contribute(source, track).IsNewTrack)
                    changes++;
            }

            Assert.Equal(1, changes);
        }

        [Fact]
        public void A_song_that_really_changes_is_still_a_new_song()
        {
            var cascade = new NowPlayingCascade();

            cascade.Contribute(MetadataSource.PlayerRemote, FromClementine(OneLine));
            cascade.Contribute(MetadataSource.WindowTitle, FromTheWindowTitle(OneLine));

            var next = cascade.Contribute(MetadataSource.PlayerRemote,
                FromClementine("Massive Attack - Teardrop"));

            Assert.True(next.IsNewTrack);
            Assert.Equal("Massive Attack", cascade.Current.Artist);
            Assert.Equal("Teardrop", cascade.Current.Title);
        }
    }
}
