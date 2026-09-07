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
    public class RadioCoverPictureTests : IDisposable
    {
        private const string ThreeReleases = """
        {"recordings":[{"title":"T.N.T.","artist-credit":[{"name":"AC/DC"}],
          "releases":[
            {"id":"album-1","status":"Official","release-group":{"primary-type":"Album"}},
            {"id":"album-2","status":"Official","release-group":{"primary-type":"Album"}},
            {"id":"bootleg-1","status":"Bootleg"}]}]}
        """;

        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-pictures-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> looked = new();

        public RadioCoverPictureTests() => Directory.CreateDirectory(folder);

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch (Exception) { }
        }

        private RadioCoverService Service(Func<string, bool?> picture) =>
            new(_ => { },
                (_, _) => Task.FromResult<string?>(ThreeReleases),
                TimeSpan.Zero,
                Path.Combine(folder, "radio-covers.json"),
                (url, _) =>
                {
                    lock (looked) looked.Add(url);
                    return Task.FromResult(picture(url));
                });

        private static CoverQuestion Question() =>
            RadioCoverQuestion.For(new NowPlayingTrack { Artist = "AC/DC", Title = "T.N.T." }, false);

        private static async Task<RadioCover?> Landing(RadioCoverService service, Action act)
        {
            var waiting = new TaskCompletionSource<RadioCover>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFound(object? sender, RadioCover cover) => waiting.TrySetResult(cover);

            service.Found += OnFound;
            try
            {
                act();
                var finished = await Task.WhenAny(waiting.Task,
                    Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                return finished == waiting.Task ? waiting.Task.Result : null;
            }
            finally
            {
                service.Found -= OnFound;
            }
        }

        private static string Front(string id) => MusicBrainzAnswer.FrontCover(id);

        [Fact]
        public async Task A_release_the_archive_has_no_picture_of_is_passed_over()
        {
            using var service = Service(url => url != Front("album-1"));

            var landed = await Landing(service, () => service.Ask(Question()));

            Assert.NotNull(landed);
            Assert.Equal(Front("album-2"), landed!.Value.Url);
        }

        [Fact]
        public async Task The_first_release_that_does_have_one_ends_the_search()
        {
            using var service = Service(_ => true);

            await Landing(service, () => service.Ask(Question()));

            lock (looked)
                Assert.Single(looked);
        }

        [Fact]
        public async Task A_song_no_release_has_a_picture_of_leaves_the_rings_alone()
        {
            using var service = Service(_ => false);

            var landed = await Landing(service, () => service.Ask(Question()));

            Assert.Null(landed);
            Assert.Null(service.Known(Question().Key));
        }

        [Fact]
        public async Task A_song_that_was_looked_for_in_vain_is_not_looked_for_again()
        {
            using var service = Service(_ => false);

            await Landing(service, () => service.Ask(Question()));
            var asked = 0;
            lock (looked) asked = looked.Count;

            service.Ask(Question());
            await Task.Delay(200, TestContext.Current.CancellationToken);

            lock (looked)
                Assert.Equal(asked, looked.Count);
        }

        [Fact]
        public async Task An_archive_that_did_not_answer_is_asked_again_next_time()
        {
            using var service = Service(_ => null);

            await Landing(service, () => service.Ask(Question()));
            var asked = 0;
            lock (looked) asked = looked.Count;

            service.Ask(Question());
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.Null(service.Known(Question().Key));
            lock (looked)
                Assert.True(looked.Count > asked,
                    "a track the archive stayed silent about was written off as having no cover");
        }

        [Fact]
        public async Task A_release_the_archive_stumbled_over_is_still_worth_showing()
        {
            using var service = Service(url => url == Front("album-1") ? null : (bool?)false);

            var landed = await Landing(service, () => service.Ask(Question()));

            Assert.NotNull(landed);
            Assert.Equal(Front("album-1"), landed!.Value.Url);
        }

        [Fact]
        public async Task A_release_the_archive_says_it_has_nothing_of_is_still_passed_over()
        {
            using var service = Service(url => url == Front("album-1") ? false : (bool?)true);

            var landed = await Landing(service, () => service.Ask(Question()));

            Assert.NotNull(landed);
            Assert.Equal(Front("album-2"), landed!.Value.Url);
        }
    }
}
