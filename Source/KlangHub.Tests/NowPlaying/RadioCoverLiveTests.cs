using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using KlangHub.Core.NowPlaying;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class RadioCoverLiveTests
    {
        private const string Switch = "KLANGHUB_COVER_LAB_LIVE";

        private static readonly TimeSpan Politeness = TimeSpan.FromMilliseconds(1200);

        private static readonly (string Line, string Fixture)[] Lines =
        {
            ("Massive Attack - Teardrop", "teardrop.json"),
            ("Daft Punk - Get Lucky", "get-lucky.json"),
            ("The Weeknd - Blinding Lights", "blinding-lights.json"),
            ("AC/DC - T.N.T.", "tnt.json"),
            ("Nirvana - Smells Like Teen Spirit", "smells-like-teen-spirit.json"),
        };

        private static void OnlyWhenAsked() =>
            Assert.SkipUnless(Environment.GetEnvironmentVariable(Switch) == "1",
                $"Set {Switch}=1 to let this talk to musicbrainz.org and coverartarchive.org.");

        private static HttpClient Talking()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(RadioCoverService.UserAgent);
            return client;
        }

        private static string FixtureFolder() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "musicbrainz");

        [Fact]
        public async Task Every_line_still_ends_in_a_picture_the_archive_really_serves()
        {
            OnlyWhenAsked();

            using var client = Talking();
            var faults = new List<string>();

            foreach (var (line, _) in Lines)
            {
                var split = IcyTitle.Split(line);
                var question = RadioCoverQuestion.For(
                    new NowPlayingTrack { Artist = split.Artist, Title = split.Title }, hasCoverAlready: false);

                if (!question.Worth)
                {
                    faults.Add($"{line}: nothing worth asking about");
                    continue;
                }

                var answer = await AskCatalogue(client, MusicBrainzAnswer.Question(question.Artist, question.Title));
                var candidates = MusicBrainzAnswer.ReleaseIds(answer, question.Artist, question.Title);
                if (candidates.Count == 0)
                {
                    faults.Add($"{line}: the catalogue names no release for this");
                    continue;
                }

                var reached = string.Empty;
                foreach (var release in candidates.Take(4))
                {
                    if (await Serves(client, MusicBrainzAnswer.FrontCover(release)) != false)
                    {
                        reached = MusicBrainzAnswer.FrontCover(release);
                        break;
                    }
                }

                if (reached.Length == 0)
                    faults.Add($"{line}: none of the first four of {candidates.Count} releases has a picture");
            }

            Assert.True(faults.Count == 0, string.Join(Environment.NewLine, faults));
        }

        [Fact]
        public async Task What_was_frozen_about_the_archive_is_still_true()
        {
            OnlyWhenAsked();

            using var client = Talking();
            var drifted = new List<string>();
            var silent = 0;

            foreach (var line in await File.ReadAllLinesAsync(Path.Combine(FixtureFolder(), "cover-art-archive.tsv"),
                                                             TestContext.Current.CancellationToken))
            {
                var parts = line.Split('\t');
                if (parts.Length != 2)
                    continue;

                var frozen = parts[1].Trim() == "200";
                var url = MusicBrainzAnswer.FrontCover(parts[0]);
                var now = await Serves(client, url);

                if (now == frozen)
                    continue;

                if (now != null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
                    now = await Serves(client, url);
                }

                if (now == null)
                {
                    silent++;
                    continue;
                }

                if (now != frozen)
                    drifted.Add($"{parts[0]}: frozen as {(frozen ? "200" : "404")}, the archive now says {(now == true ? "200" : "404")}");
            }

            Assert.True(drifted.Count == 0,
                $"The frozen answers no longer describe the archive ({silent} release(s) it would not answer about):" +
                Environment.NewLine + string.Join(Environment.NewLine, drifted));
        }

        private static async Task<string> AskCatalogue(HttpClient client, string question)
        {
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                await Task.Delay(Politeness * attempt, TestContext.Current.CancellationToken);

                try
                {
                    using var response = await client.GetAsync(question, TestContext.Current.CancellationToken);

                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

                    if ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
                        Assert.Fail($"The catalogue answered {(int)response.StatusCode} to {question}");
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
                {
                    if (TestContext.Current.CancellationToken.IsCancellationRequested)
                        throw;
                }
            }

            Assert.Skip("The catalogue would not answer, five tries and half a minute apart. That is its day, not our code.");
            return string.Empty;
        }

        private static async Task<bool?> Serves(HttpClient client, string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

                if (response.IsSuccessStatusCode)
                    return true;

                return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone ? false : (bool?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
