using System;
using System.Collections.Generic;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>Which ways of finding out what is playing the user allows.</summary>
    public sealed class NowPlayingOptions
    {
        public bool UseFileTags { get; init; } = true;

        public bool UseSystemMediaControls { get; init; } = true;

        /// <summary>Path to the now-playing text file, or empty when the user set none up.</summary>
        public string NowPlayingFilePath { get; init; } = string.Empty;
    }

    /// <summary>
    /// Runs the sources and feeds everything they say into one cascade.
    /// <para>
    /// The loopback stream KlangHub casts has no title, no artist and no cover - it is an endless tone. This
    /// is where a title comes from at all, and it is the whole reason an evening with Clementine can look
    /// like a Roon endpoint on the television instead of saying "Default Media Receiver".
    /// </para>
    /// </summary>
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

        /// <summary>The merged best answer, and the event that says it changed.</summary>
        public NowPlayingCascade Cascade { get; } = new();

        /// <summary>Raised when the merged answer changed - what the stage and the LOAD follow.</summary>
        public event EventHandler<NowPlayingTrack>? Changed;

        /// <summary>True when a track change happened, i.e. the receiver needs a fresh LOAD, not a nudge.</summary>
        public event EventHandler<NowPlayingTrack>? TrackChanged;

        public void Start(NowPlayingOptions options)
        {
            Stop();

            useFileTags = options.UseFileTags;

            if (options.UseSystemMediaControls)
                Add(new SystemMediaControlsSource(log));

            // The player's own title bar. Rides along with the Windows-now-playing switch on purpose: to a
            // listener both are simply "read what the player is showing", and splitting them into two
            // settings would be a developer's distinction on a page that must not have any.
            if (options.UseSystemMediaControls)
                Add(new WindowTitleSource(log));

            // Clementine's own remote, if it is switched on over there. No setting and no port to type:
            // it tries the standard port and stays quiet when nothing answers, so a listener who has it
            // enabled gets a full screen and one who has not loses nothing.
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
                    // A source that cannot start is a source that stays quiet; the others carry the stage.
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

        /// <summary>
        /// Feeds one contribution in, then follows up on the one fact that unlocks the rest: if a path has
        /// become known, the file name and the tags on the disc are read and fed in as well.
        /// </summary>
        public void Contribute(MetadataSource source, NowPlayingTrack track)
        {
            if (disposed)
                return;

            CascadeResult result;
            lock (gate)
            {
                result = Cascade.Contribute(source, track);

                var path = Cascade.Current.FilePath;
                if (path != null)
                {
                    var guessed = nameFollower.Follow(path);
                    if (guessed != null)
                        Cascade.Contribute(MetadataSource.FileName, guessed);

                    // The cover follows the file, not the title: the picture is either inside this file or
                    // lying next to it. Nothing found leaves the branded artwork in place - a deliberate
                    // screen rather than a broken one.
                    var hunted = coverFollower.Follow(path);
                    if (hunted != null)
                        CurrentCover.Set(hunted.CoverBytes);

                    if (useFileTags)
                    {
                        var tagged = tagFollower.Follow(path);
                        if (tagged != null)
                            result = Cascade.Contribute(MetadataSource.FileTags, tagged) with { IsNewTrack = result.IsNewTrack };
                    }
                }
            }

            if (!result.Changed)
                return;

            var current = Cascade.Current;
            Changed?.Invoke(this, current);
            if (result.IsNewTrack)
                TrackChanged?.Invoke(this, current);
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
