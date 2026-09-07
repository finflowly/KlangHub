using KlangHub.Core.NowPlaying;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class NowPlayingCoverTests
    {
        private static readonly byte[] Picture = { 0xFF, 0xD8, 0xFF, 0x01, 0x02, 0x03 };

        private static NowPlayingService Service() => new(_ => { });

        [Fact]
        public void A_track_that_brings_a_picture_puts_it_on_the_stage()
        {
            using var service = Service();

            service.Contribute(MetadataSource.PlayerRemote,
                new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack", CoverBytes = Picture });

            Assert.Equal(Picture, service.Cover);
        }

        [Fact]
        public void A_stream_without_a_picture_does_not_keep_the_one_from_the_file_before_it()
        {
            using var service = Service();

            service.Contribute(MetadataSource.PlayerRemote,
                new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack", CoverBytes = Picture });
            service.Contribute(MetadataSource.PlayerRemote,
                new NowPlayingTrack { Title = "Some Station Is Playing Something" });

            Assert.Null(service.Cover);
        }

        [Fact]
        public void A_second_word_about_the_same_track_leaves_its_picture_alone()
        {
            using var service = Service();

            service.Contribute(MetadataSource.PlayerRemote,
                new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack", CoverBytes = Picture });
            service.Contribute(MetadataSource.PlayerRemote,
                new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack", Album = "Mezzanine" });

            Assert.Equal(Picture, service.Cover);
        }

        [Fact]
        public void The_stage_is_told_when_a_picture_arrives_and_when_it_goes()
        {
            using var service = Service();
            var seen = 0;
            service.CoverChanged += (_, _) => seen++;

            service.Contribute(MetadataSource.PlayerRemote,
                new NowPlayingTrack { Title = "Teardrop", CoverBytes = Picture });
            service.Contribute(MetadataSource.PlayerRemote, new NowPlayingTrack { Title = "A Station" });

            Assert.Equal(2, seen);
        }
    }
}
