using System.Collections.Generic;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// The moment the cascade earns its keep: some source finally reveals where the file lives, and that
    /// one fact unlocks the two best answers there are - the tags on the disc and the cover beside them.
    /// This is what turns "Clementine tells the cast nothing" into a full stage.
    /// </summary>
    public class PathFollowerTests
    {
        [Fact]
        public void Reads_the_file_as_soon_as_a_path_appears()
        {
            var read = new List<string>();
            var follower = new PathFollower(path => { read.Add(path); return new NowPlayingTrack { Title = "Teardrop" }; });

            follower.Follow(@"D:\Musik\03.flac");

            Assert.Equal(new[] { @"D:\Musik\03.flac" }, read);
        }

        [Fact]
        public void Reads_each_file_only_once()
        {
            // Sources repeat themselves constantly - SMTC alone fires several times per track. Re-reading
            // tags off the disc on every one of those would hit the disc for nothing.
            var reads = 0;
            var follower = new PathFollower(_ => { reads++; return new NowPlayingTrack(); });

            follower.Follow(@"D:\Musik\03.flac");
            follower.Follow(@"D:\Musik\03.flac");
            follower.Follow(@"D:\Musik\03.flac");

            Assert.Equal(1, reads);
        }

        [Fact]
        public void Reads_again_when_the_file_changes()
        {
            var reads = 0;
            var follower = new PathFollower(_ => { reads++; return new NowPlayingTrack(); });

            follower.Follow(@"D:\Musik\03.flac");
            follower.Follow(@"D:\Musik\04.flac");

            Assert.Equal(2, reads);
        }

        [Fact]
        public void Ignores_a_path_that_only_differs_in_spelling()
        {
            var reads = 0;
            var follower = new PathFollower(_ => { reads++; return new NowPlayingTrack(); });

            follower.Follow(@"D:\Musik\03.flac");
            follower.Follow(@"d:\musik\03.FLAC");

            Assert.Equal(1, reads);
        }

        [Fact]
        public void Hands_back_what_it_learned()
        {
            var follower = new PathFollower(_ => new NowPlayingTrack { Title = "Teardrop", Album = "Mezzanine" });

            var learned = follower.Follow(@"D:\Musik\03.flac");

            Assert.Equal("Teardrop", learned?.Title);
            Assert.Equal("Mezzanine", learned?.Album);
        }

        [Fact]
        public void Says_nothing_when_there_is_no_path_yet()
        {
            var reads = 0;
            var follower = new PathFollower(_ => { reads++; return new NowPlayingTrack(); });

            Assert.Null(follower.Follow(null));
            Assert.Null(follower.Follow("   "));
            Assert.Equal(0, reads);
        }
    }
}
