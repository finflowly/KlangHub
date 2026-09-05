using System;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// Watches for the one fact that unlocks everything else: where the file lives.
    /// <para>
    /// Any source may reveal it - a now-playing file with a <c>$file</c> line, a player that fills the path
    /// into its session. The moment it appears, the tags on the disc and the cover beside them become
    /// readable, and a track that was a bare title turns into a full stage. That is the difference between
    /// "Clementine tells the cast nothing" and an evening that looks like a Roon endpoint.
    /// </para>
    /// Reads each file once. Sources repeat themselves several times per track, and going back to the disc
    /// for every repeat would buy nothing.
    /// </summary>
    public sealed class PathFollower
    {
        private readonly Func<string, NowPlayingTrack> read;
        private string? lastPath;

        public PathFollower(Func<string, NowPlayingTrack> readIn)
        {
            read = readIn;
        }

        /// <summary>What the file had to say, or null when there is nothing new to ask.</summary>
        public NowPlayingTrack? Follow(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var trimmed = path.Trim();
            if (string.Equals(trimmed, lastPath, StringComparison.OrdinalIgnoreCase))
                return null;

            lastPath = trimmed;
            return read(trimmed);
        }

        /// <summary>Forget the file we last read, so the next mention of it is read afresh.</summary>
        public void Reset() => lastPath = null;
    }
}
