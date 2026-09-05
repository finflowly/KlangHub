using System;
using System.IO;
using System.Text;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    /// <summary>
    /// Reading the now-playing file off the disc. Whoever writes that file chose their own encoding and
    /// their own idea of file sharing, and KlangHub gets no say in either - so the reader has to cope with
    /// all of it and never throw, because the alternative is the stage going blank mid-album.
    /// </summary>
    public class NowPlayingFileReaderTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-nowplaying-" + Guid.NewGuid().ToString("N"));

        public NowPlayingFileReaderTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a temp folder that stays is not worth failing a test over */ }
        }

        private string WriteFile(string name, string content, Encoding encoding)
        {
            var path = Path.Combine(folder, name);
            File.WriteAllText(path, content, encoding);
            return path;
        }

        [Fact]
        public void Reads_utf8_without_a_byte_order_mark()
        {
            var path = WriteFile("np.txt", "Björk - Jóga", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var track = NowPlayingFileReader.Read(path);

            Assert.Equal("Björk", track.Artist);
            Assert.Equal("Jóga", track.Title);
        }

        [Fact]
        public void Reads_utf8_with_a_byte_order_mark()
        {
            var path = WriteFile("np.txt", "Björk - Jóga", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var track = NowPlayingFileReader.Read(path);

            Assert.Equal("Björk", track.Artist);
            Assert.Equal("Jóga", track.Title);
        }

        [Fact]
        public void Reads_utf16()
        {
            // What a PowerShell helper writes by default - and a file that would otherwise arrive as
            // "B\0j\0ö\0r\0k\0" and put line noise on the television.
            var path = WriteFile("np.txt", "Björk - Jóga", new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

            var track = NowPlayingFileReader.Read(path);

            Assert.Equal("Björk", track.Artist);
            Assert.Equal("Jóga", track.Title);
        }

        [Fact]
        public void A_file_that_is_not_there_says_nothing()
        {
            // The user pointed at a file their helper has not created yet. That is normal, not an error.
            var track = NowPlayingFileReader.Read(Path.Combine(folder, "does-not-exist.txt"));

            Assert.True(track.IsEmpty);
        }

        [Fact]
        public void An_empty_file_says_nothing()
        {
            var path = WriteFile("np.txt", string.Empty, Encoding.UTF8);

            Assert.True(NowPlayingFileReader.Read(path).IsEmpty);
        }

        [Fact]
        public void Reads_a_file_the_writer_still_has_open()
        {
            // A helper rewriting the file every few seconds holds it open while doing so. Refusing to read
            // it would mean the stage only updates when the helper happens to be idle.
            var path = WriteFile("np.txt", "Massive Attack - Teardrop", Encoding.UTF8);

            using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            Assert.Equal("Teardrop", NowPlayingFileReader.Read(path).Title);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void No_path_says_nothing(string? path)
        {
            Assert.True(NowPlayingFileReader.Read(path).IsEmpty);
        }
    }
}
