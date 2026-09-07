using System;
using System.IO;
using System.Text;
using System.Threading;
using KlangHub.Core.NowPlaying;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class NowPlayingServiceTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-service-" + Guid.NewGuid().ToString("N"));

        public NowPlayingServiceTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch {  }
        }

        private string WriteTaggedWav(string name, string title, string artist, string album)
        {
            var path = Path.Combine(folder, name);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                const int bitDepth = 16, sampleRate = 44100, samples = 44100;
                var dataBytes = samples * (bitDepth / 8);
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * bitDepth / 8);
                writer.Write((short)(bitDepth / 8));
                writer.Write((short)bitDepth);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                writer.Write(new byte[dataBytes]);
            }

            var tags = new ATL.Track(path) { Title = title, Artist = artist, Album = album };
            tags.Save();
            return path;
        }

        [Fact]
        public void A_now_playing_file_naming_a_track_gives_the_stage_everything()
        {
            var audio = WriteTaggedWav("song.wav", "Teardrop", "Massive Attack", "Mezzanine");
            var nowPlaying = Path.Combine(folder, "NowPlaying.txt");
            File.WriteAllText(nowPlaying, "file=" + audio, Encoding.UTF8);

            using var service = new NowPlayingService(_ => { });
            service.Start(new NowPlayingOptions { NowPlayingFilePath = nowPlaying, UseSystemMediaControls = false });

            Assert.Equal("Teardrop", service.Cascade.Current.Title);
            Assert.Equal("Massive Attack", service.Cascade.Current.Artist);
            Assert.Equal("Mezzanine", service.Cascade.Current.Album);
            Assert.Equal(44100, service.Cascade.Current.SampleRate);
        }

        [Fact]
        public void A_now_playing_file_without_a_path_still_names_the_track()
        {
            var nowPlaying = Path.Combine(folder, "NowPlaying.txt");
            File.WriteAllText(nowPlaying, "Massive Attack - Teardrop", Encoding.UTF8);

            using var service = new NowPlayingService(_ => { });
            service.Start(new NowPlayingOptions { NowPlayingFilePath = nowPlaying, UseSystemMediaControls = false });

            Assert.Equal("Teardrop", service.Cascade.Current.Title);
            Assert.Equal("Massive Attack", service.Cascade.Current.Artist);
        }

        [Fact]
        public void Announces_a_new_track_when_the_file_moves_on()
        {
            var first = WriteTaggedWav("first.wav", "Teardrop", "Massive Attack", "Mezzanine");
            var second = WriteTaggedWav("second.wav", "Xtal", "Aphex Twin", "Selected Ambient Works");

            using var service = new NowPlayingService(_ => { });
            var announced = new ManualResetEventSlim();
            NowPlayingTrack? newTrack = null;
            service.Updated += (_, update) => { if (!update.IsNewTrack) return; newTrack = update.Track; announced.Set(); };

            service.Contribute(MetadataSource.NowPlayingFile, new NowPlayingTrack { FilePath = first });
            announced.Reset();
            service.Contribute(MetadataSource.NowPlayingFile, new NowPlayingTrack { FilePath = second });

            Assert.True(announced.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            Assert.Equal("Xtal", newTrack?.Title);
            Assert.Equal("Aphex Twin", newTrack?.Artist);
            Assert.Equal("Selected Ambient Works", newTrack?.Album);
        }

        [Fact]
        public void Turning_the_tag_reader_off_leaves_the_weaker_answers_standing()
        {
            var audio = WriteTaggedWav("Massive Attack - Teardrop.wav", "Ignoriert", "Ignoriert", "Ignoriert");

            using var service = new NowPlayingService(_ => { });
            service.Start(new NowPlayingOptions { UseFileTags = false, UseSystemMediaControls = false });
            service.Contribute(MetadataSource.NowPlayingFile, new NowPlayingTrack { FilePath = audio });

            Assert.Equal("Teardrop", service.Cascade.Current.Title);
            Assert.Equal("Massive Attack", service.Cascade.Current.Artist);
        }

        [Fact]
        public void Starting_with_no_sources_at_all_is_quiet_not_broken()
        {
            using var service = new NowPlayingService(_ => { });
            service.Start(new NowPlayingOptions { UseSystemMediaControls = false });

            Assert.True(service.Cascade.Current.IsEmpty);
        }
    }
}
