using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KlangHub.Core.NowPlaying;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class RadioCoverCatalogueTests : IDisposable
    {
        private static readonly Dictionary<string, bool> Archive = ReadArchive();

        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-catalogue-" + Guid.NewGuid().ToString("N"));

        public RadioCoverCatalogueTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch (Exception) { }
        }

        private static string FixtureFolder() =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "musicbrainz");

        private static string Answer(string fixture) =>
            File.ReadAllText(Path.Combine(FixtureFolder(), fixture));

        private static Dictionary<string, bool> ReadArchive()
        {
            var known = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var line in File.ReadAllLines(Path.Combine(FixtureFolder(), "cover-art-archive.tsv")))
            {
                var parts = line.Split('\t');
                if (parts.Length == 2 && parts[0].Length > 0)
                    known[parts[0]] = parts[1].Trim() == "200";
            }

            return known;
        }

        private static bool HasAPicture(string url)
        {
            var id = url.Replace("https://coverartarchive.org/release/", string.Empty, StringComparison.Ordinal)
                        .Replace("/front-500", string.Empty, StringComparison.Ordinal);

            return Archive.TryGetValue(id, out var yes) && yes;
        }

        private static NowPlayingTrack AsClementineWouldSendIt(string line)
        {
            var split = IcyTitle.Split(line);
            return new NowPlayingTrack { Artist = split.Artist, Title = split.Title };
        }

        private async Task<string?> CoverFor(string line, string fixture)
        {
            var question = RadioCoverQuestion.For(AsClementineWouldSendIt(line), hasCoverAlready: false);
            if (!question.Worth)
                return null;

            using var service = new RadioCoverService(
                _ => { },
                (_, _) => Task.FromResult<string?>(Answer(fixture)),
                TimeSpan.Zero,
                Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json"),
                (url, _) => Task.FromResult<bool?>(HasAPicture(url)));

            var waiting = new TaskCompletionSource<RadioCover>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Found += (_, cover) => waiting.TrySetResult(cover);
            service.Ask(question);

            var finished = await Task.WhenAny(waiting.Task,
                Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            return finished == waiting.Task ? waiting.Task.Result.Url : null;
        }

        [Theory]
        [InlineData("Massive Attack - Teardrop", "teardrop.json")]
        [InlineData("Daft Punk - Get Lucky", "get-lucky.json")]
        [InlineData("The Weeknd - Blinding Lights", "blinding-lights.json")]
        [InlineData("AC/DC - T.N.T.", "tnt.json")]
        [InlineData("Nirvana - Smells Like Teen Spirit", "smells-like-teen-spirit.json")]
        public async Task A_line_the_radio_sends_ends_in_a_picture_that_is_really_there(string line, string fixture)
        {
            var url = await CoverFor(line, fixture);

            Assert.NotNull(url);
            Assert.StartsWith("https://coverartarchive.org/release/", url);
            Assert.EndsWith("/front-500", url);
            Assert.True(HasAPicture(url!), $"{line} was sent to {url}, and there is no picture there");
        }

        private static readonly (string Line, string Fixture)[] Lines =
        {
            ("Massive Attack - Teardrop", "teardrop.json"),
            ("Daft Punk - Get Lucky", "get-lucky.json"),
            ("The Weeknd - Blinding Lights", "blinding-lights.json"),
            ("AC/DC - T.N.T.", "tnt.json"),
            ("Nirvana - Smells Like Teen Spirit", "smells-like-teen-spirit.json"),
        };

        private static string? FirstChoice(string line, string fixture)
        {
            var split = IcyTitle.Split(line);
            return MusicBrainzAnswer.ReleaseId(Answer(fixture), split.Artist!, split.Title!);
        }

        [Fact]
        public async Task A_first_choice_the_archive_has_no_picture_of_does_not_end_the_search()
        {
            var blind = Lines
                .Select(l => (l.Line, l.Fixture, First: FirstChoice(l.Line, l.Fixture)))
                .Where(l => l.First != null && !HasAPicture(MusicBrainzAnswer.FrontCover(l.First!)))
                .ToList();

            Assert.True(blind.Count > 0,
                "No frozen answer leads with a release the archive has no picture of any more. " +
                "That is what these fixtures are here to prove, so a fresh one is needed.");

            foreach (var (line, fixture, first) in blind)
            {
                var url = await CoverFor(line, fixture);

                Assert.NotNull(url);
                Assert.NotEqual(MusicBrainzAnswer.FrontCover(first!), url);
                Assert.True(HasAPicture(url!), $"{line} was sent to {url}, and there is no picture there");
            }
        }

        [Fact]
        public async Task A_song_whose_releases_all_lack_a_picture_leaves_the_rings_alone()
        {
            var url = await CoverFor("Massive Attack - Teardrop", "teardrop.json");

            using var nothing = new RadioCoverService(
                _ => { },
                (_, _) => Task.FromResult<string?>(Answer("teardrop.json")),
                TimeSpan.Zero,
                Path.Combine(folder, "none.json"),
                (_, _) => Task.FromResult<bool?>(false));

            var waiting = new TaskCompletionSource<RadioCover>(TaskCreationOptions.RunContinuationsAsynchronously);
            nothing.Found += (_, cover) => waiting.TrySetResult(cover);
            nothing.Ask(RadioCoverQuestion.For(AsClementineWouldSendIt("Massive Attack - Teardrop"), false));

            var finished = await Task.WhenAny(waiting.Task,
                Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

            Assert.NotNull(url);
            Assert.NotEqual(waiting.Task, finished);
        }

        [Theory]
        [InlineData("Massive Attack - Teardrop", "Massive Attack", "Teardrop")]
        [InlineData("Jean-Michel Jarre - Oxygene", "Jean-Michel Jarre", "Oxygene")]
        [InlineData("AC/DC - T.N.T.", "AC/DC", "T.N.T.")]
        public void A_line_with_one_separator_is_split_where_it_belongs(string line, string artist, string title)
        {
            var split = IcyTitle.Split(line);

            Assert.True(split.Certain);
            Assert.Equal(artist, split.Artist);
            Assert.Equal(title, split.Title);
            Assert.True(RadioCoverQuestion.For(AsClementineWouldSendIt(line), false).Worth);
        }

        [Theory]
        [InlineData("A - B - C")]
        [InlineData("T.N.T.")]
        [InlineData("Now playing on the station")]
        [InlineData("https://stream.example.invalid/live")]
        [InlineData("")]
        public void A_line_that_names_no_artist_is_never_asked_about(string line)
        {
            Assert.False(RadioCoverQuestion.For(AsClementineWouldSendIt(line), false).Worth);
        }

        [Fact]
        public void The_catalogue_is_asked_the_very_question_these_answers_came_from()
        {
            Assert.Equal(
                "https://musicbrainz.org/ws/2/recording?fmt=json&limit=10&query=" +
                Uri.EscapeDataString("artist:\"Massive Attack\" AND recording:\"Teardrop\""),
                MusicBrainzAnswer.Question("Massive Attack", "Teardrop"));
        }

        [Fact]
        public void The_dots_in_TNT_are_not_what_tells_two_songs_apart()
        {
            Assert.True(CoverMatch.Same("T.N.T.", "TNT"));

            var candidates = MusicBrainzAnswer.ReleaseIds(Answer("tnt.json"), "AC/DC", "TNT");

            Assert.NotEmpty(candidates);
            Assert.Equal(MusicBrainzAnswer.ReleaseIds(Answer("tnt.json"), "AC/DC", "T.N.T."), candidates);
        }

        [Fact]
        public void Every_answer_that_was_frozen_still_names_releases_this_code_can_use()
        {
            foreach (var (line, fixture) in Lines)
            {
                var split = IcyTitle.Split(line);
                var candidates = MusicBrainzAnswer.ReleaseIds(Answer(fixture), split.Artist!, split.Title!);

                Assert.True(candidates.Count > 1, $"{line}: only {candidates.Count} release(s) to try");
                Assert.Contains(candidates, id => HasAPicture(MusicBrainzAnswer.FrontCover(id)));
                Assert.Equal(candidates.Distinct(), candidates);
            }
        }
    }
}
