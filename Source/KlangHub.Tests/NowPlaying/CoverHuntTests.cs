using System;
using System.IO;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// Finding a picture for the piece that is playing. The rule from the prompt is absolute: never leave a
    /// generic placeholder standing while an actual cover exists somewhere - so this looks in the file, then
    /// beside it, and only gives up when there is genuinely nothing.
    /// </summary>
    public class CoverHuntTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-cover-" + Guid.NewGuid().ToString("N"));

        public CoverHuntTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a temp folder that stays is not worth failing a test over */ }
        }

        /// <summary>The smallest thing that is unmistakably a PNG.</summary>
        private static byte[] TinyPng() => Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        private string WriteWav(string name)
        {
            var path = Path.Combine(folder, name);
            const int bitDepth = 16, sampleRate = 44100, samples = 4410;
            var dataBytes = samples * (bitDepth / 8);
            using var writer = new BinaryWriter(File.Create(path));
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
            return path;
        }

        [Fact]
        public void Finds_a_picture_embedded_in_the_file()
        {
            var audio = WriteWav("song.wav");
            var track = new ATL.Track(audio);
            track.EmbeddedPictures.Add(ATL.PictureInfo.fromBinaryData(TinyPng()));
            track.Save();

            var cover = CoverHunt.Find(audio);

            Assert.NotNull(cover);
            Assert.NotEmpty(cover!);
        }

        [Theory]
        [InlineData("cover.jpg")]
        [InlineData("folder.jpg")]
        [InlineData("Artwork.jpg")]
        [InlineData("front.png")]
        [InlineData("COVER.PNG")]
        public void Finds_a_picture_lying_next_to_the_file(string coverName)
        {
            var audio = WriteWav("song.wav");
            File.WriteAllBytes(Path.Combine(folder, coverName), TinyPng());

            Assert.NotNull(CoverHunt.Find(audio));
        }

        [Fact]
        public void Prefers_the_picture_inside_the_file()
        {
            // The embedded picture belongs to this track; the one in the folder belongs to the album, and on
            // a compilation those are not the same thing.
            var audio = WriteWav("song.wav");
            var embedded = TinyPng();
            var track = new ATL.Track(audio);
            track.EmbeddedPictures.Add(ATL.PictureInfo.fromBinaryData(embedded));
            track.Save();
            File.WriteAllBytes(Path.Combine(folder, "cover.jpg"), new byte[] { 1, 2, 3, 4, 5 });

            var cover = CoverHunt.Find(audio);

            Assert.NotEqual(new byte[] { 1, 2, 3, 4, 5 }, cover);
        }

        [Fact]
        public void Nothing_to_find_means_nothing_returned()
        {
            // The caller then keeps the branded artwork - a deliberate screen, not a broken one.
            Assert.Null(CoverHunt.Find(WriteWav("song.wav")));
        }

        [Fact]
        public void Ignores_a_stray_file_that_is_not_a_picture()
        {
            var audio = WriteWav("song.wav");
            File.WriteAllText(Path.Combine(folder, "cover.jpg"), string.Empty);

            Assert.Null(CoverHunt.Find(audio));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void No_path_means_nothing_returned(string? path)
        {
            Assert.Null(CoverHunt.Find(path));
        }

        [Fact]
        public void A_path_that_does_not_exist_means_nothing_returned()
        {
            Assert.Null(CoverHunt.Find(Path.Combine(folder, "nope.flac")));
        }
    }
}
