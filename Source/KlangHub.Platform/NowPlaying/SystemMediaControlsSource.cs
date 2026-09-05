using System;
using System.Threading.Tasks;
using KlangHub.Core.NowPlaying;
using Windows.Media.Control;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// Windows' own now-playing session - the one the media keys and the volume flyout talk to. Most modern
    /// players fill it in, which is what lets KlangHub name a track it is only hearing as loopback audio.
    /// <para>
    /// Deliberately thin. It reads properties and hands them to <see cref="SmtcReading"/>; every decision
    /// about what those properties mean lives in Core, where it is tested. WinRT cannot be constructed in a
    /// test, so the less that happens here, the less is unproven.
    /// </para>
    /// <para>
    /// Nothing in here is allowed to throw. The session manager is unavailable on some Windows editions and
    /// in some session states, and a player can vanish between two property reads - none of that may take
    /// the audio down with it.
    /// </para>
    /// </summary>
    public sealed class SystemMediaControlsSource : INowPlayingSource
    {
        private readonly Action<string> log;
        private GlobalSystemMediaTransportControlsSessionManager? manager;
        private GlobalSystemMediaTransportControlsSession? session;
        private bool disposed;

        public SystemMediaControlsSource(Action<string> logIn)
        {
            log = logIn;
        }

        public MetadataSource Source => MetadataSource.SystemMediaControls;

        public event EventHandler<NowPlayingTrack>? Reported;

        public void Start()
        {
            _ = StartAsync();
        }

        private async Task StartAsync()
        {
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                manager.CurrentSessionChanged += OnCurrentSessionChanged;
                AttachToCurrentSession();
            }
            catch (Exception ex)
            {
                // Not fatal by design: the cascade has four other ways to learn what is playing.
                log($"now-playing: Windows session manager unavailable ({ex.Message}) - carrying on without it");
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
            => AttachToCurrentSession();

        /// <summary>
        /// Follows whichever player Windows considers current. Only one session is watched at a time: with
        /// two players open, the one the user last touched is the one being cast.
        /// </summary>
        private void AttachToCurrentSession()
        {
            if (disposed)
                return;

            try
            {
                Detach();

                session = manager?.GetCurrentSession();
                if (session == null)
                    return;

                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                Report();
            }
            catch (Exception ex)
            {
                log($"now-playing: could not follow the current Windows session ({ex.Message})");
            }
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => Report();

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => Report();

        private void Report()
        {
            _ = ReportAsync();
        }

        private async Task ReportAsync()
        {
            var current = session;
            if (current == null || disposed)
                return;

            try
            {
                var properties = await current.TryGetMediaPropertiesAsync();
                if (properties == null)
                    return;

                var timeline = current.GetTimelineProperties();

                var reading = new SmtcReading
                {
                    Title = properties.Title,
                    Artist = properties.Artist,
                    AlbumTitle = properties.AlbumTitle,
                    AlbumArtist = properties.AlbumArtist,
                    Duration = timeline.EndTime - timeline.StartTime
                };

                var track = reading.ToTrack();
                if (!track.IsEmpty)
                    Reported?.Invoke(this, track);
            }
            catch (Exception ex)
            {
                // A player closing between two reads lands here. Nothing to repair - the next event will
                // bring a fresh reading, and the cascade still holds what we knew.
                log($"now-playing: could not read the Windows session ({ex.Message})");
            }
        }

        private void Detach()
        {
            if (session == null)
                return;

            try
            {
                session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            catch (Exception ex)
            {
                log($"now-playing: could not let go of the previous Windows session ({ex.Message})");
            }

            session = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Detach();

            try
            {
                if (manager != null)
                    manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }
            catch (Exception ex)
            {
                log($"now-playing: could not let go of the Windows session manager ({ex.Message})");
            }

            manager = null;
        }
    }
}
