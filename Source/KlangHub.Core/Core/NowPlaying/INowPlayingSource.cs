using System;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// One way of finding out what is playing. A source knows nothing about the others: it watches whatever
    /// it watches and reports what it saw, and <see cref="NowPlayingCascade"/> decides what that is worth.
    /// </summary>
    public interface INowPlayingSource : IDisposable
    {
        /// <summary>How much its answers are to be believed.</summary>
        MetadataSource Source { get; }

        /// <summary>Raised whenever the source has something to say. Repeats are fine - the cascade filters.</summary>
        event EventHandler<NowPlayingTrack>? Reported;

        /// <summary>Begin watching. Must not throw: a source that cannot start is a source that stays quiet.</summary>
        void Start();
    }
}
