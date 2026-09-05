using System;
using System.IO;
using System.Threading;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// Watches the now-playing text file the user pointed KlangHub at.
    /// <para>
    /// Two mechanisms, on purpose. The watcher gives an update within a heartbeat of the helper writing the
    /// file, which is what makes the stage feel attached to the music. The slow poll behind it is the safety
    /// net: <see cref="FileSystemWatcher"/> misses changes on network shares, on some synchronising folders,
    /// and when a helper replaces the file instead of rewriting it - and a stage that silently stops
    /// following the music is worse than one that follows it a few seconds late.
    /// </para>
    /// The debounce is what keeps a half-written file from being read as a new track.
    /// </summary>
    public sealed class NowPlayingFileSource : INowPlayingSource
    {
        /// <summary>Long enough for a helper to finish writing, short enough to feel immediate.</summary>
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

        /// <summary>The safety net behind the watcher, not the primary mechanism.</summary>
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        private readonly string path;
        private readonly Action<string> log;
        private FileSystemWatcher? watcher;
        private Timer? debounce;
        private Timer? poll;
        private bool disposed;

        public NowPlayingFileSource(string pathIn, Action<string> logIn)
        {
            path = pathIn;
            log = logIn;
        }

        public MetadataSource Source => MetadataSource.NowPlayingFile;

        public event EventHandler<NowPlayingTrack>? Reported;

        public void Start()
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            debounce = new Timer(_ => ReadAndReport(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            poll = new Timer(_ => ReadAndReport(), null, PollInterval, PollInterval);

            try
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(path));
                var fileName = Path.GetFileName(path);
                if (folder != null && Directory.Exists(folder))
                {
                    watcher = new FileSystemWatcher(folder, fileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };
                    watcher.Changed += OnFileTouched;
                    watcher.Created += OnFileTouched;
                    watcher.Renamed += OnFileTouched;
                }
            }
            catch (Exception ex)
            {
                // The poll still runs, so this costs freshness rather than the feature.
                log($"now-playing: cannot watch '{path}' ({ex.Message}) - falling back to polling it");
            }

            // Read once straight away: the file usually already holds the track that is playing.
            ReadAndReport();
        }

        private void OnFileTouched(object sender, FileSystemEventArgs e)
        {
            // Restart the timer rather than read now. A helper writing the file produces a burst of events,
            // and the ones in the middle of that burst show a half-written file.
            try { debounce?.Change(Debounce, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
        }

        private void ReadAndReport()
        {
            if (disposed)
                return;

            var track = NowPlayingFileReader.Read(path);
            if (!track.IsEmpty)
                Reported?.Invoke(this, track);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            try
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Changed -= OnFileTouched;
                    watcher.Created -= OnFileTouched;
                    watcher.Renamed -= OnFileTouched;
                    watcher.Dispose();
                }
            }
            catch (Exception ex)
            {
                log($"now-playing: could not stop watching '{path}' ({ex.Message})");
            }

            watcher = null;
            debounce?.Dispose();
            debounce = null;
            poll?.Dispose();
            poll = null;
        }
    }
}
