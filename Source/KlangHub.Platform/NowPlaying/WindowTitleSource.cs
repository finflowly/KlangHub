using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// Watches the title bars of running music players.
    /// <para>
    /// Measured on 2026-09-05: Clementine, mid-track, reports nothing at all to Windows' now-playing
    /// session, while its title bar reads "Aquanote - Nowhere (Speakeasy remix)". Without this source, a
    /// listener casting from such a player has to set up a helper script before the television knows what
    /// is on; with it, it simply works.
    /// </para>
    /// <para>
    /// Polled rather than event-driven: a title bar has no change notification that does not involve
    /// hooking another process, and two seconds is close enough for a screen whose transitions take one.
    /// </para>
    /// </summary>
    public sealed class WindowTitleSource : INowPlayingSource
    {
        /// <summary>
        /// Players worth looking at, by process name. A deliberately closed list: reading the title of
        /// every window on the machine would put a browser tab or a document name on the television.
        /// </summary>
        private static readonly string[] Players =
        {
            "clementine", "foobar2000", "aimp", "musicbee", "winamp", "deadbeef",
            "audacious", "quodlibet", "strawberry", "mpc-hc64", "mpc-hc", "mpc-be64", "mpc-be", "vlc"
        };

        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

        private readonly Action<string> log;
        private Timer? timer;
        private string lastTitle = string.Empty;
        private bool disposed;

        public WindowTitleSource(Action<string> logIn)
        {
            log = logIn;
        }

        public MetadataSource Source => MetadataSource.WindowTitle;

        public event EventHandler<NowPlayingTrack>? Reported;

        public void Start()
        {
            timer = new Timer(_ => Look(), null, TimeSpan.Zero, Interval);
        }

        private void Look()
        {
            if (disposed)
                return;

            try
            {
                foreach (var name in Players)
                {
                    var process = Process.GetProcessesByName(name).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle));
                    if (process == null)
                        continue;

                    var title = process.MainWindowTitle;
                    // Only when it actually changed: the same title arriving every two seconds would keep
                    // the cascade busy for nothing.
                    if (title == lastTitle)
                        return;

                    lastTitle = title;

                    var track = PlayerWindowTitle.Parse(name, title);
                    if (!track.IsEmpty)
                        Reported?.Invoke(this, track);

                    return;
                }
            }
            catch (Exception ex)
            {
                // Enumerating processes can fail transiently, and a process can exit between the listing
                // and the read. Neither is worth a word on screen.
                log($"now-playing: could not read a player's window title ({ex.Message})");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            timer?.Dispose();
            timer = null;
        }
    }
}
