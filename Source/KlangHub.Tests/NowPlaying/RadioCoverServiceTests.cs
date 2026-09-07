using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KlangHub.Core.NowPlaying;
using KlangHub.Platform.NowPlaying;
using Xunit;

namespace KlangHub.Tests.NowPlaying
{
    public class RadioCoverServiceTests : IDisposable
    {
        private const string Found = """
        {"recordings":[{"title":"Teardrop","artist-credit":[{"name":"Massive Attack"}],
          "releases":[{"id":"album-1","status":"Official","release-group":{"primary-type":"Album"}}]}]}
        """;

        private const string Nothing = """{"recordings":[]}""";

        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-covers-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> asked = new();

        public RadioCoverServiceTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch (Exception) { }
        }

        private string Remembered => Path.Combine(folder, "radio-covers.json");

        private RadioCoverService Service(Func<string, string?> answer) =>
            new(_ => { },
                (url, _) =>
                {
                    lock (asked) asked.Add(url);
                    return Task.FromResult(answer(url));
                },
                TimeSpan.Zero,
                Remembered);

        private static CoverQuestion Question(string artist = "Massive Attack", string title = "Teardrop") =>
            RadioCoverQuestion.For(new NowPlayingTrack { Artist = artist, Title = title }, false);

        private static async Task<RadioCover?> Landing(RadioCoverService service, Action act)
        {
            var waiting = new TaskCompletionSource<RadioCover>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFound(object? sender, RadioCover cover) => waiting.TrySetResult(cover);

            service.Found += OnFound;
            try
            {
                act();
                var finished = await Task.WhenAny(waiting.Task, Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                return finished == waiting.Task ? waiting.Task.Result : null;
            }
            finally
            {
                service.Found -= OnFound;
            }
        }

        [Fact]
        public async Task A_track_the_catalogue_knows_gets_a_cover_after_the_fact()
        {
            using var service = Service(_ => Found);
            var question = Question();

            var landed = await Landing(service, () => service.Ask(question));

            Assert.NotNull(landed);
            Assert.Equal("https://coverartarchive.org/release/album-1/front-500", landed!.Value.Url);
            Assert.Equal(question.Key, landed.Value.Key);
        }

        [Fact]
        public async Task A_track_the_catalogue_does_not_know_leaves_the_rings_alone()
        {
            using var service = Service(_ => Nothing);

            var landed = await Landing(service, () => service.Ask(Question()));

            Assert.Null(landed);
            Assert.Null(service.Known(Question().Key));
        }

        [Fact]
        public async Task A_catalogue_that_answers_with_somebody_else_is_not_believed()
        {
            using var service = Service(_ => Found);

            var landed = await Landing(service, () => service.Ask(Question("Newton Faulkner", "Teardrop")));

            Assert.Null(landed);
        }

        [Fact]
        public async Task The_same_track_is_only_ever_asked_about_once()
        {
            using var service = Service(_ => Found);

            await Landing(service, () => service.Ask(Question()));
            service.Ask(Question());
            service.Ask(Question("  massive attack  ", "TEARDROP"));
            await Task.Delay(200, TestContext.Current.CancellationToken);

            lock (asked)
                Assert.Single(asked);
        }

        [Fact]
        public async Task A_miss_is_remembered_so_the_catalogue_is_not_hammered()
        {
            using var service = Service(_ => Nothing);

            await Landing(service, () => service.Ask(Question()));
            service.Ask(Question());
            await Task.Delay(200, TestContext.Current.CancellationToken);

            lock (asked)
                Assert.Single(asked);
        }

        [Fact]
        public async Task A_cover_that_arrives_after_the_song_has_moved_on_is_dropped()
        {
            var slow = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var service = new RadioCoverService(
                _ => { },
                (_, _) => { started.TrySetResult(true); return slow.Task; },
                TimeSpan.Zero,
                Remembered);

            RadioCover? landed = null;
            service.Found += (_, cover) => landed = cover;

            service.Ask(Question());
            await started.Task;
            service.Forget();
            slow.SetResult(Found);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.Null(landed);
            Assert.Equal("https://coverartarchive.org/release/album-1/front-500", service.Known(Question().Key));
        }

        [Fact]
        public async Task What_was_learned_once_survives_a_restart()
        {
            using (var service = Service(_ => Found))
                await Landing(service, () => service.Ask(Question()));

            using var again = Service(_ => throw new InvalidOperationException("must not ask again"));

            Assert.Equal("https://coverartarchive.org/release/album-1/front-500", again.Known(Question().Key));
        }

        [Fact]
        public async Task A_catalogue_asking_us_to_wait_is_not_asked_again_straight_away()
        {
            using var service = Service(_ => throw new TooManyQuestionsException());

            service.Ask(Question());
            await Task.Delay(300, TestContext.Current.CancellationToken);
            service.Ask(Question("Portishead", "Glory Box"));
            await Task.Delay(300, TestContext.Current.CancellationToken);

            lock (asked)
                Assert.Single(asked);
        }

        [Fact]
        public async Task A_catalogue_that_is_simply_broken_costs_nothing_but_the_cover()
        {
            using var service = Service(_ => throw new InvalidOperationException("no network"));

            var landed = await Landing(service, () => service.Ask(Question()));

            Assert.Null(landed);
        }

        [Fact]
        public void A_question_not_worth_asking_is_never_asked()
        {
            using var service = Service(_ => Found);

            service.Ask(RadioCoverQuestion.For(new NowPlayingTrack { Title = "Radio Paradise" }, false));

            lock (asked)
                Assert.Empty(asked);
        }
    }
}
