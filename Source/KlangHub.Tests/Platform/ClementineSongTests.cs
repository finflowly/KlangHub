using System;
using KlangHub.Platform.Clementine;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Turning what Clementine's network remote hands over into what the stage needs.
    /// <para>
    /// This source is the richest one there is for a Clementine listener - it carries the album, the
    /// length, the file and the cover art as bytes, none of which a window title or a now-playing file can
    /// provide. Measured on 2026-09-05, it is also the only way to get any of it: Clementine reports
    /// nothing at all to Windows' own now-playing session.
    /// </para>
    /// </summary>
    public class ClementineSongTests
    {
        [Fact]
        public void Carries_the_piece_across()
        {
            var song = new SongMetadata { Title = "Nowhere", Artist = "Aquanote", Album = "Expressions" };

            var track = ClementineSong.ToTrack(song);

            Assert.Equal("Nowhere", track.Title);
            Assert.Equal("Aquanote", track.Artist);
            Assert.Equal("Expressions", track.Album);
        }

        [Fact]
        public void Falls_back_to_the_album_artist()
        {
            var song = new SongMetadata { Title = "Nowhere", Albumartist = "Various Artists" };

            Assert.Equal("Various Artists", ClementineSong.ToTrack(song).Artist);
        }

        [Fact]
        public void Prefers_the_track_artist()
        {
            var song = new SongMetadata { Title = "Nowhere", Artist = "Aquanote", Albumartist = "Various Artists" };

            Assert.Equal("Aquanote", ClementineSong.ToTrack(song).Artist);
        }

        [Fact]
        public void Reads_the_length_in_seconds()
        {
            var song = new SongMetadata { Title = "Nowhere", Length = 337 };

            Assert.Equal(TimeSpan.FromSeconds(337), ClementineSong.ToTrack(song).Duration);
        }

        [Fact]
        public void A_length_of_zero_is_not_a_length()
        {
            // An internet radio stream reports zero. Passing it on would draw a finished progress line
            // under something that has no end.
            var song = new SongMetadata { Title = "Some Radio", Length = 0 };

            Assert.Null(ClementineSong.ToTrack(song).Duration);
        }

        [Theory]
        [InlineData(2, "FLAC")]
        [InlineData(5, "MP3")]
        [InlineData(3, "MP4")]
        [InlineData(8, "OGG")]
        [InlineData(10, "WAV")]
        [InlineData(13, "OPUS")]
        [InlineData(17, "APE")]
        public void Names_the_format_the_way_a_listener_would(int clementineType, string expected)
        {
            var song = new SongMetadata { Title = "Nowhere", Type = clementineType };

            Assert.Equal(expected, ClementineSong.ToTrack(song).Format);
        }

        [Theory]
        [InlineData(0)]    // UNKNOWN
        [InlineData(99)]   // STREAM
        public void Says_nothing_about_a_format_it_cannot_name(int clementineType)
        {
            var song = new SongMetadata { Title = "Nowhere", Type = clementineType };

            Assert.Null(ClementineSong.ToTrack(song).Format);
        }

        [Fact]
        public void Turns_a_file_url_back_into_a_path()
        {
            // Clementine sends file:///D:/Musik/... - the tag reader and the cover hunt need a real path.
            var song = new SongMetadata { Title = "Nowhere", Url = "file:///D:/Musik/Aquanote/03%20Nowhere.flac" };

            Assert.Equal(@"D:\Musik\Aquanote\03 Nowhere.flac", ClementineSong.ToTrack(song).FilePath);
        }

        [Fact]
        public void A_stream_url_is_not_a_file_path()
        {
            // An internet radio has a URL but no file. Handing that to the tag reader would be nonsense.
            var song = new SongMetadata { Title = "Some Radio", Url = "http://stream.example/live.mp3" };

            Assert.Null(ClementineSong.ToTrack(song).FilePath);
        }

        [Fact]
        public void Brings_the_cover_along()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 };
            var song = new SongMetadata { Title = "Nowhere", Art = Google.Protobuf.ByteString.CopyFrom(png) };

            Assert.Equal(png, ClementineSong.ToTrack(song).CoverBytes);
        }

        [Fact]
        public void No_cover_is_no_cover_rather_than_an_empty_one()
        {
            var song = new SongMetadata { Title = "Nowhere" };

            Assert.Null(ClementineSong.ToTrack(song).CoverBytes);
        }

        [Fact]
        public void An_empty_song_says_nothing()
        {
            Assert.True(ClementineSong.ToTrack(new SongMetadata()).IsEmpty);
        }

        [Fact]
        public void Nothing_at_all_says_nothing()
        {
            Assert.True(ClementineSong.ToTrack(null).IsEmpty);
        }
    }
}
