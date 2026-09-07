using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    public readonly record struct RadioCover(string Key, string Url);

    public sealed class TooManyQuestionsException : Exception
    {
    }

    public sealed class RadioCoverService : IDisposable
    {
        public const string UserAgent = "KlangHub/0.0.1 ( https://github.com/finflowly/KlangHub )";

        private static readonly TimeSpan Politeness = TimeSpan.FromMilliseconds(1100);
        private static readonly TimeSpan BackOff = TimeSpan.FromMinutes(5);
        private const int MostRemembered = 400;
        private const int MostTried = 4;

        private readonly Func<string, CancellationToken, Task<string?>> ask;
        private readonly Func<string, CancellationToken, Task<bool?>>? hasPicture;
        private readonly Action<string> log;
        private readonly TimeSpan politeness;
        private readonly string? remembered;
        private readonly Dictionary<string, string> known = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim oneAtATime = new(1, 1);
        private readonly CancellationTokenSource closing = new();
        private readonly object gate = new();

        private DateTime lastAsked = DateTime.MinValue;
        private DateTime silentUntil = DateTime.MinValue;
        private string wanted = string.Empty;
        private bool disposed;

        public RadioCoverService(Action<string> logIn)
            : this(logIn, Talking())
        {
        }

        private RadioCoverService(Action<string> logIn, HttpClient client)
            : this(logIn, Over(client), Politeness, RememberedAt(), Reachable(client))
        {
        }

        public RadioCoverService(Action<string> logIn,
                                 Func<string, CancellationToken, Task<string?>> askIn,
                                 TimeSpan politenessIn,
                                 string? rememberedIn,
                                 Func<string, CancellationToken, Task<bool?>>? hasPictureIn = null)
        {
            log = logIn;
            ask = askIn;
            politeness = politenessIn;
            remembered = rememberedIn;
            hasPicture = hasPictureIn;
            Recall();
        }

        public event EventHandler<RadioCover>? Found;

        public string? Known(string? key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            lock (gate)
                return known.TryGetValue(key, out var url) && url.Length > 0 ? url : null;
        }

        public void Forget() => Ask(CoverQuestion.None);

        public void Ask(CoverQuestion question)
        {
            if (disposed)
                return;

            lock (gate)
            {
                wanted = question.Worth ? question.Key : string.Empty;

                if (!question.Worth || known.ContainsKey(question.Key))
                    return;
            }

            _ = Task.Run(() => Hunt(question), closing.Token);
        }

        private async Task Hunt(CoverQuestion question)
        {
            try
            {
                await oneAtATime.WaitAsync(closing.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                lock (gate)
                {
                    if (!string.Equals(wanted, question.Key, StringComparison.Ordinal) || known.ContainsKey(question.Key))
                        return;

                    if (DateTime.UtcNow < silentUntil)
                        return;
                }

                var wait = politeness - (DateTime.UtcNow - lastAsked);
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, closing.Token).ConfigureAwait(false);

                lock (gate)
                {
                    if (!string.Equals(wanted, question.Key, StringComparison.Ordinal))
                        return;
                }

                lastAsked = DateTime.UtcNow;
                var answer = await ask(MusicBrainzAnswer.Question(question.Artist, question.Title), closing.Token)
                    .ConfigureAwait(false);

                var found = await FirstWithAPicture(
                    MusicBrainzAnswer.ReleaseIds(answer, question.Artist, question.Title)).ConfigureAwait(false);

                var url = found.Url;

                if (found.Certain)
                    Remember(question.Key, url);
                else
                    log("radio cover: the archive did not say either way; this track is not written off");

                if (url.Length == 0)
                {
                    log("radio cover: nothing certain for this track");
                    return;
                }

                bool still;
                lock (gate)
                    still = string.Equals(wanted, question.Key, StringComparison.Ordinal);

                if (still)
                    Found?.Invoke(this, new RadioCover(question.Key, url));
            }
            catch (OperationCanceledException)
            {
            }
            catch (TooManyQuestionsException)
            {
                lock (gate)
                    silentUntil = DateTime.UtcNow + BackOff;

                log("radio cover: the catalogue asked us to wait; not asking again for a while");
            }
            catch (Exception ex)
            {
                log($"radio cover: lookup failed ({ex.Message})");
            }
            finally
            {
                try { oneAtATime.Release(); } catch (Exception) { }
            }
        }

        private readonly record struct Picture(string Url, bool Certain);

        private async Task<Picture> FirstWithAPicture(IReadOnlyList<string> releases)
        {
            var tried = 0;

            foreach (var release in releases)
            {
                if (tried++ >= MostTried)
                {
                    log($"radio cover: {releases.Count} releases to try, stopped after {MostTried}");
                    break;
                }

                var url = MusicBrainzAnswer.FrontCover(release);

                if (hasPicture == null)
                    return new Picture(url, true);

                var there = await hasPicture(url, closing.Token).ConfigureAwait(false);

                if (there == true)
                    return new Picture(url, true);

                if (there == null)
                    return new Picture(url, false);
            }

            return new Picture(string.Empty, true);
        }

        private void Remember(string key, string url)
        {
            lock (gate)
            {
                if (known.Count >= MostRemembered)
                    known.Clear();

                known[key] = url;
            }

            Write();
        }

        private static HttpClient Talking()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return client;
        }

        private static Func<string, CancellationToken, Task<bool?>> Reachable(HttpClient client) =>
            async (url, token) =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, url);
                    using var response = await client.SendAsync(request, token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                        return true;

                    return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
                        ? false
                        : (bool?)null;
                }
                catch (Exception)
                {
                    return null;
                }
            };

        private static Func<string, CancellationToken, Task<string?>> Over(HttpClient client)
        {
            return async (url, token) =>
            {
                using var response = await client.GetAsync(url, token).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new TooManyQuestionsException();

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            };
        }

        private static string? RememberedAt()
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KlangHub");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "radio-covers.json");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Recall()
        {
            if (remembered == null || !File.Exists(remembered))
                return;

            try
            {
                var read = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(remembered));
                if (read == null)
                    return;

                lock (gate)
                    foreach (var pair in read)
                        known[pair.Key] = pair.Value ?? string.Empty;
            }
            catch (Exception ex)
            {
                log($"radio cover: could not read what was remembered ({ex.Message})");
            }
        }

        private void Write()
        {
            if (remembered == null)
                return;

            try
            {
                Dictionary<string, string> copy;
                lock (gate)
                    copy = new Dictionary<string, string>(known, StringComparer.Ordinal);

                File.WriteAllText(remembered, JsonSerializer.Serialize(copy));
            }
            catch (Exception ex)
            {
                log($"radio cover: could not remember ({ex.Message})");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            try { closing.Cancel(); } catch (Exception) { }

            closing.Dispose();
            oneAtATime.Dispose();
        }
    }
}
