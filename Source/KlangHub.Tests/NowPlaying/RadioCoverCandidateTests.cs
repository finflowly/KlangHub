using System.Linq;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class RadioCoverCandidateTests
    {
        private const string ABootlegBeforeTheAlbum = """
        {"recordings":[{"title":"T.N.T.","artist-credit":[{"name":"AC/DC"}],
          "releases":[
            {"id":"bootleg-1","status":"Bootleg"},
            {"id":"single-1","status":"Official","release-group":{"primary-type":"Single"}},
            {"id":"album-1","status":"Official","release-group":{"primary-type":"Album"}},
            {"id":"album-2","status":"Official","release-group":{"primary-type":"Album"}}]}]}
        """;

        private const string TwoRecordings = """
        {"recordings":[
          {"title":"Someone Else","artist-credit":[{"name":"Massive Attack"}],
           "releases":[{"id":"wrong-1","status":"Official","release-group":{"primary-type":"Album"}}]},
          {"title":"Teardrop","artist-credit":[{"name":"Massive Attack"}],
           "releases":[{"id":"first-1","status":"Official","release-group":{"primary-type":"Album"}}]},
          {"title":"Teardrop (Remastered)","artist-credit":[{"name":"Massive Attack"}],
           "releases":[{"id":"first-1","status":"Official"},{"id":"second-1","status":"Official"}]}]}
        """;

        [Fact]
        public void The_album_leads_and_the_rest_follows_it()
        {
            var candidates = MusicBrainzAnswer.ReleaseIds(ABootlegBeforeTheAlbum, "AC/DC", "T.N.T.");

            Assert.Equal(new[] { "album-1", "album-2", "single-1", "bootleg-1" }, candidates);
        }

        [Fact]
        public void The_one_release_that_was_chosen_before_is_still_the_one_that_leads()
        {
            var candidates = MusicBrainzAnswer.ReleaseIds(ABootlegBeforeTheAlbum, "AC/DC", "T.N.T.");

            Assert.Equal(MusicBrainzAnswer.ReleaseId(ABootlegBeforeTheAlbum, "AC/DC", "T.N.T."), candidates.First());
        }

        [Fact]
        public void Every_recording_that_matches_offers_what_it_has()
        {
            var candidates = MusicBrainzAnswer.ReleaseIds(TwoRecordings, "Massive Attack", "Teardrop");

            Assert.Equal(new[] { "first-1", "second-1" }, candidates);
            Assert.DoesNotContain("wrong-1", candidates);
        }

        [Fact]
        public void A_release_is_never_offered_twice()
        {
            var candidates = MusicBrainzAnswer.ReleaseIds(TwoRecordings, "Massive Attack", "Teardrop");

            Assert.Equal(candidates.Distinct(), candidates);
        }

        [Theory]
        [InlineData("""{"recordings":[]}""")]
        [InlineData("""{"recordings":[{"title":"Teardrop","artist-credit":[{"name":"Newton Faulkner"}],"releases":[{"id":"x"}]}]}""")]
        [InlineData("not json at all")]
        [InlineData("")]
        public void Nothing_certain_offers_nothing(string json)
        {
            Assert.Empty(MusicBrainzAnswer.ReleaseIds(json, "Massive Attack", "Teardrop"));
        }
    }
}
