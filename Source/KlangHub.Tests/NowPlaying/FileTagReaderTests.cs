using System;
using System.IO;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// Reading the tags of the file itself - the most honest source in the cascade, because it is what is
    /// actually written on the disc rather than what a player decided to say about it. It is also the only
    /// source that can name the codec, the sample rate and the bit depth, which is what earns the quality
    /// mark in the corner of the stage.
    /// </summary>
    public class FileTagReaderTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-tags-" + Guid.NewGuid().ToString("N"));

        public FileTagReaderTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a temp folder that stays is not worth failing a test over */ }
        }

        /// <summary>A real, playable 16-bit 44.1 kHz WAV of a fraction of a second, with tags written in.</summary>
        private string WriteTaggedWav(string name, string? title, string? artist, string? album)
        {
            var path = Path.Combine(folder, name);
            WriteSilentWav(path, sampleRate: 44100, bitDepth: 16, samples: 4410);

            var track = new ATL.Track(path);
            if (title != null) track.Title = title;
            if (artist != null) track.Artist = artist;
            if (album != null) track.Album = album;
            track.Save();

            return path;
        }

        private static void WriteSilentWav(string path, int sampleRate, short bitDepth, int samples)
        {
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
            writer.Write(bitDepth);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
        }

        [Fact]
        public void Reads_what_is_written_on_the_disc()
        {
            var path = WriteTaggedWav("track.wav", "Teardrop", "Massive Attack", "Mezzanine");

            var track = FileTagReader.Read(path);

            Assert.Equal("Teardrop", track.Title);
            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Mezzanine", track.Album);
        }

        [Fact]
        public void Carries_the_path_it_read()
        {
            var path = WriteTaggedWav("track.wav", "Teardrop", null, null);

            Assert.Equal(path, FileTagReader.Read(path).FilePath);
        }

        [Fact]
        public void Reads_the_technical_facts_behind_the_quality_mark()
        {
            var path = WriteTaggedWav("track.wav", "Teardrop", null, null);

            var track = FileTagReader.Read(path);

            Assert.Equal(44100, track.SampleRate);
            Assert.Equal(16, track.BitDepth);
            Assert.False(string.IsNullOrWhiteSpace(track.Format));
        }

        [Fact]
        public void An_untagged_file_yields_no_invented_title()
        {
            // ATL falls back to the file name for an untagged file. That guess belongs to the file-name
            // source and its rank - passing it off as a tag would let it outrank better answers.
            var path = Path.Combine(folder, "03 Teardrop.wav");
            WriteSilentWav(path, 44100, 16, 4410);

            Assert.Null(FileTagReader.Read(path).Title);
        }

        [Fact]
        public void A_file_that_is_not_there_says_nothing()
        {
            Assert.True(FileTagReader.Read(Path.Combine(folder, "nope.flac")).IsEmpty);
        }

        [Fact]
        public void Something_that_is_not_audio_says_nothing()
        {
            // The path came from a now-playing file the user wrote. It may point at anything.
            var path = Path.Combine(folder, "notes.txt");
            File.WriteAllText(path, "this is not a song");

            Assert.True(FileTagReader.Read(path).IsEmpty);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void No_path_says_nothing(string? path)
        {
            Assert.True(FileTagReader.Read(path).IsEmpty);
        }
    }
}
