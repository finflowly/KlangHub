using System;
using KlangHub.Platform.Clementine;
using Xunit;

namespace KlangHub.Tests.Platform
{
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
        [InlineData(0)]
        [InlineData(99)]
        public void Says_nothing_about_a_format_it_cannot_name(int clementineType)
        {
            var song = new SongMetadata { Title = "Nowhere", Type = clementineType };

            Assert.Null(ClementineSong.ToTrack(song).Format);
        }

        [Fact]
        public void Turns_a_file_url_back_into_a_path()
        {
            var song = new SongMetadata { Title = "Nowhere", Url = "file:///D:/Musik/Aquanote/03%20Nowhere.flac" };

            Assert.Equal(@"D:\Musik\Aquanote\03 Nowhere.flac", ClementineSong.ToTrack(song).FilePath);
        }

        [Fact]
        public void A_stream_url_is_not_a_file_path()
        {
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

        [Fact]
        public void A_stream_that_names_both_in_one_line_reaches_the_stage_taken_apart()
        {
            var song = new SongMetadata { Title = "Massive Attack - Teardrop", Type = 99 };

            var track = ClementineSong.ToTrack(song);

            Assert.Equal("Massive Attack", track.Artist);
            Assert.Equal("Teardrop", track.Title);
        }

        [Fact]
        public void A_stream_that_already_names_the_artist_is_left_as_it_is()
        {
            var song = new SongMetadata { Title = "Well - Known - Song", Artist = "Someone", Type = 99 };

            var track = ClementineSong.ToTrack(song);

            Assert.Equal("Someone", track.Artist);
            Assert.Equal("Well - Known - Song", track.Title);
        }

        [Fact]
        public void A_song_name_carrying_a_hyphen_keeps_it()
        {
            var song = new SongMetadata { Title = "Ballad Of A Well-Known Gun", Type = 99 };

            var track = ClementineSong.ToTrack(song);

            Assert.Null(track.Artist);
            Assert.Equal("Ballad Of A Well-Known Gun", track.Title);
        }
    }
}
