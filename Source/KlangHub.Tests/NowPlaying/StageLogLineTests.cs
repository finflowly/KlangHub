using System;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class StageLogLineTests
    {
        private static StageUpdate Update(string? artist, string? title, string? cover, bool isNewTrack) =>
            StageUpdate.For(new NowPlayingTrack { Artist = artist, Title = title },
                            zone: null, coverUrl: cover, isNewTrack)!;

        [Fact]
        public void A_line_names_who_and_what_and_whether_it_is_a_new_track()
        {
            var line = Update("Massive Attack", "Teardrop", null, true).Describe();

            Assert.Contains("Massive Attack", line);
            Assert.Contains("Teardrop", line);
            Assert.Contains("new track", line);
        }

        [Fact]
        public void A_refinement_says_that_it_is_one()
        {
            Assert.Contains("same track", Update("Daft Punk", "Get Lucky", null, false).Describe());
        }

        [Fact]
        public void The_address_of_the_picture_is_never_written_down()
        {
            var line = Update("Daft Punk", "Get Lucky", "https://coverartarchive.org/release/album-1/front-500", false)
                .Describe();

            Assert.DoesNotContain("http", line);
            Assert.DoesNotContain("coverartarchive", line);
        }

        [Fact]
        public void That_there_is_a_picture_at_all_is_worth_writing_down()
        {
            Assert.Contains("with a cover",
                Update("Daft Punk", "Get Lucky", "https://coverartarchive.org/release/album-1/front-500", false)
                    .Describe());

            Assert.Contains("no cover", Update("Daft Punk", "Get Lucky", null, false).Describe());
        }

        [Fact]
        public void A_track_that_names_nobody_still_gives_a_line_that_reads()
        {
            var line = Update(null, "Broken Nights", null, true).Describe();

            Assert.Contains("Broken Nights", line);
            Assert.DoesNotContain("  ", line);
        }

        [Fact]
        public void A_line_stays_one_line()
        {
            var line = Update("Massive Attack", "Tear\r\ndrop", null, true).Describe();

            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
        }
    }
}
