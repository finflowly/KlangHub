using System;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class CoverMatchTests
    {
        [Theory]
        [InlineData("Massive Attack", "massive attack")]
        [InlineData("Sigur Rós", "Sigur Ros")]
        [InlineData("Teardrop (Remastered)", "Teardrop")]
        [InlineData("Teardrop [2019 Remaster]", "teardrop")]
        [InlineData("Song feat. Someone", "Song")]
        [InlineData("Song ft. Someone", "song")]
        [InlineData("Rock'n'Roll", "Rock n Roll")]
        [InlineData("T.N.T.", "TNT")]
        [InlineData("R.E.M.", "REM")]
        [InlineData("Lovesong", "Love Song")]
        public void The_same_song_under_a_different_spelling_is_still_the_same_song(string one, string other)
        {
            Assert.True(CoverMatch.Same(one, other));
        }

        [Theory]
        [InlineData("Teardrop", "Tear")]
        [InlineData("Massive Attack", "Massive Attac")]
        [InlineData("", "Teardrop")]
        [InlineData("   ", "")]
        public void A_different_song_is_not_waved_through(string one, string other)
        {
            Assert.False(CoverMatch.Same(one, other));
        }
    }

    public class RadioCoverQuestionTests
    {
        private static NowPlayingTrack Radio(string artist, string title) =>
            new() { Artist = artist, Title = title };

        [Fact]
        public void A_radio_track_that_names_both_is_worth_asking_about()
        {
            var question = RadioCoverQuestion.For(Radio("Massive Attack", "Teardrop"), hasCoverAlready: false);

            Assert.True(question.Worth);
            Assert.Equal("massive attack|teardrop", question.Key);
        }

        [Fact]
        public void The_same_track_spelled_differently_asks_the_same_question_once()
        {
            Assert.Equal(
                RadioCoverQuestion.For(Radio("Massive Attack", "Teardrop"), false).Key,
                RadioCoverQuestion.For(Radio("  massive   attack ", "TEARDROP"), false).Key);
        }

        [Fact]
        public void A_track_that_already_has_a_picture_is_not_asked_about()
        {
            Assert.False(RadioCoverQuestion.For(Radio("Massive Attack", "Teardrop"), hasCoverAlready: true).Worth);
        }

        [Fact]
        public void A_file_on_this_machine_is_not_asked_about()
        {
            var track = new NowPlayingTrack { Artist = "A", Title = "B", FilePath = @"C:\music\b.m4a" };

            Assert.False(RadioCoverQuestion.For(track, false).Worth);
        }

        [Theory]
        [InlineData("Radio Paradise", "")]
        [InlineData("", "Teardrop")]
        [InlineData("https://stream.example.invalid/live", "Teardrop")]
        [InlineData("Massive Attack", "www.example.invalid")]
        public void A_station_name_or_an_address_is_not_a_song(string artist, string title)
        {
            Assert.False(RadioCoverQuestion.For(Radio(artist, title), false).Worth);
        }

        [Fact]
        public void Nothing_at_all_is_not_asked_about()
        {
            Assert.False(RadioCoverQuestion.For(null, false).Worth);
        }
    }

    public class MusicBrainzAnswerTests
    {
        private const string Found = """
        {"recordings":[
          {"title":"Teardrop","artist-credit":[{"name":"Massive Attack"}],
           "releases":[{"id":"single-1","status":"Official","release-group":{"primary-type":"Single"}},
                       {"id":"album-1","status":"Official","release-group":{"primary-type":"Album"}}]}]}
        """;

        [Fact]
        public void The_album_a_song_belongs_to_is_preferred_over_the_single()
        {
            Assert.Equal("album-1", MusicBrainzAnswer.ReleaseId(Found, "Massive Attack", "Teardrop"));
        }

        [Fact]
        public void A_release_nobody_calls_official_is_only_the_last_resort()
        {
            const string json = """
            {"recordings":[{"title":"Teardrop","artist-credit":[{"name":"Massive Attack"}],
              "releases":[{"id":"bootleg-1","status":"Pseudo-Release"},{"id":"official-1","status":"Official"}]}]}
            """;

            Assert.Equal("official-1", MusicBrainzAnswer.ReleaseId(json, "Massive Attack", "Teardrop"));
        }

        [Fact]
        public void A_recording_by_somebody_else_is_not_this_song()
        {
            Assert.Null(MusicBrainzAnswer.ReleaseId(Found, "Newton Faulkner", "Teardrop"));
        }

        [Fact]
        public void A_recording_with_another_name_is_not_this_song()
        {
            Assert.Null(MusicBrainzAnswer.ReleaseId(Found, "Massive Attack", "Angel"));
        }

        [Fact]
        public void A_recording_nothing_was_ever_released_on_gives_nothing()
        {
            const string json = """
            {"recordings":[{"title":"Teardrop","artist-credit":[{"name":"Massive Attack"}]}]}
            """;

            Assert.Null(MusicBrainzAnswer.ReleaseId(json, "Massive Attack", "Teardrop"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("{}")]
        [InlineData("{\"recordings\":\"nonsense\"}")]
        public void An_answer_that_is_not_an_answer_is_no_reason_to_fall_over(string json)
        {
            Assert.Null(MusicBrainzAnswer.ReleaseId(json, "Massive Attack", "Teardrop"));
        }

        [Fact]
        public void The_question_names_both_and_survives_a_quotation_mark()
        {
            var url = MusicBrainzAnswer.Question("AC/DC", "Rock \"n\" Roll");

            Assert.StartsWith("https://musicbrainz.org/ws/2/recording?fmt=json&limit=10&query=", url);
            Assert.DoesNotContain("\"n\"", url);
            Assert.Contains(Uri.EscapeDataString("AC/DC"), url);
        }

        [Fact]
        public void A_release_is_turned_into_the_address_of_its_front_cover()
        {
            Assert.Equal("https://coverartarchive.org/release/album-1/front-500",
                MusicBrainzAnswer.FrontCover("album-1"));
        }
    }
}
