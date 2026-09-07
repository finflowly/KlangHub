using System;
using System.Collections.Generic;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    public sealed class NowPlayingOptions
    {
        public bool UseFileTags { get; init; } = true;

        public bool UseSystemMediaControls { get; init; } = true;

        public string NowPlayingFilePath { get; init; } = string.Empty;
    }

    public sealed class NowPlayingService : IDisposable
    {
        private readonly Action<string> log;
        private readonly List<INowPlayingSource> sources = new();
        private readonly PathFollower tagFollower;
        private readonly PathFollower nameFollower;
        private readonly PathFollower coverFollower;
        private readonly object gate = new();
        private bool useFileTags = true;
        private bool disposed;

        public NowPlayingService(Action<string> logIn)
        {
            log = logIn;
            tagFollower = new PathFollower(FileTagReader.Read);
            nameFollower = new PathFollower(FileNameGuess.Parse);
            coverFollower = new PathFollower(path => new NowPlayingTrack { CoverBytes = CoverHunt.Find(path) });
        }

        public NowPlayingCascade Cascade { get; } = new();

        public byte[]? Cover { get; private set; }

        public event EventHandler<NowPlayingUpdate>? Updated;

        public event EventHandler<byte[]?>? CoverChanged;

        public void Start(NowPlayingOptions options)
        {
            Stop();

            useFileTags = options.UseFileTags;

            if (options.UseSystemMediaControls)
                Add(new SystemMediaControlsSource(log));

            if (options.UseSystemMediaControls)
                Add(new WindowTitleSource(log));

            if (options.UseSystemMediaControls)
                Add(new Clementine.ClementineRemoteSource(log));

            if (!string.IsNullOrWhiteSpace(options.NowPlayingFilePath))
                Add(new NowPlayingFileSource(options.NowPlayingFilePath, log));

            foreach (var source in sources)
            {
                try
                {
                    source.Start();
                }
                catch (Exception ex)
                {
                    log($"now-playing: source {source.Source} did not start ({ex.Message})");
                }
            }
        }

        private void Add(INowPlayingSource source)
        {
            source.Reported += OnReported;
            sources.Add(source);
        }

        private void OnReported(object? sender, NowPlayingTrack track)
        {
            if (sender is not INowPlayingSource source)
                return;

            Contribute(source.Source, track);
        }

        public void Contribute(MetadataSource source, NowPlayingTrack track)
        {
            if (disposed)
                return;

            CascadeResult result;
            byte[]? coverBefore;
            lock (gate)
            {
                result = Cascade.Contribute(source, track);
                coverBefore = Cover;

                if (result.IsNewTrack)
                {
                    coverFollower.Reset();
                    SetCover(null);
                }

                if (track.CoverBytes is { Length: > 0 })
                    SetCover(track.CoverBytes);

                var path = Cascade.Current.FilePath;
                if (path != null)
                {
                    var guessed = nameFollower.Follow(path);
                    if (guessed != null)
                        Cascade.Contribute(MetadataSource.FileName, guessed);

                    var hunted = coverFollower.Follow(path);
                    if (hunted?.CoverBytes is { Length: > 0 })
                        SetCover(hunted.CoverBytes);

                    if (useFileTags)
                    {
                        var tagged = tagFollower.Follow(path);
                        if (tagged != null)
                            result = Cascade.Contribute(MetadataSource.FileTags, tagged) with { IsNewTrack = result.IsNewTrack };
                    }
                }
            }

            if (!ReferenceEquals(coverBefore, Cover))
                CoverChanged?.Invoke(this, Cover);

            if (!result.Changed)
                return;

            Updated?.Invoke(this, new NowPlayingUpdate(Cascade.Current, result.IsNewTrack));
        }

        private void SetCover(byte[]? bytes)
        {
            Cover = bytes is { Length: > 0 } ? bytes : null;
            CurrentCover.Set(Cover);
        }

        public void Stop()
        {
            foreach (var source in sources)
            {
                try
                {
                    source.Reported -= OnReported;
                    source.Dispose();
                }
                catch (Exception ex)
                {
                    log($"now-playing: source {source.Source} did not stop cleanly ({ex.Message})");
                }
            }

            sources.Clear();
            SetCover(null);
            tagFollower.Reset();
            nameFollower.Reset();
            coverFollower.Reset();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
        }
    }
}
