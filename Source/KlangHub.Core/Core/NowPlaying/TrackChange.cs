using System;
using System.Linq;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// Decides when the stage is allowed to cut to a new scene.
    /// <para>
    /// The rule is deliberately reluctant. Metadata arrives in pieces - a file name first, the artist two
    /// seconds later, the album when the tags have been read - and every one of those arrivals looks like
    /// "something changed". Until the live transport exists, a new track means a fresh LOAD, and a fresh
    /// LOAD is an audible gap. So a track counts as new only when the evidence contradicts what we knew,
    /// never when it merely adds to it.
    /// </para>
    /// </summary>
    public static class TrackChange
    {
        /// <summary>Two lengths this close apart are the same length; sources round differently.</summary>
        private static readonly TimeSpan LengthWobble = TimeSpan.FromSeconds(2);

        public static bool IsNewTrack(NowPlayingTrack? previous, NowPlayingTrack next)
        {
            if (next == null || next.IsEmpty)
                return false;

            if (previous == null || previous.IsEmpty)
                return true;

            // A different file is a different track even under the same name - the live version after the
            // studio one is not the same recording.
            if (Contradicts(previous.FilePath, next.FilePath, PathsDiffer))
                return true;

            if (Contradicts(previous.Title, next.Title, TextDiffers))
                return true;

            if (Contradicts(previous.Artist, next.Artist, TextDiffers))
                return true;

            if (previous.Duration != null && next.Duration != null &&
                (previous.Duration.Value - next.Duration.Value).Duration() > LengthWobble)
                return true;

            return false;
        }

        /// <summary>
        /// Only a value we had AND still have can contradict. Learning one we lacked is enrichment; losing
        /// one we had is a source dropping out, and neither may blank the stage.
        /// </summary>
        private static bool Contradicts(string? before, string? after, Func<string, string, bool> differ)
        {
            if (string.IsNullOrWhiteSpace(before) || string.IsNullOrWhiteSpace(after))
                return false;

            return differ(before, after);
        }

        private static bool TextDiffers(string a, string b) =>
            !string.Equals(Normalise(a), Normalise(b), StringComparison.Ordinal);

        private static bool PathsDiffer(string a, string b) =>
            !string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Lower case, single spaces, trimmed. Sources disagree about capitals and stray spaces constantly,
        /// and none of that disagreement is a new song.
        /// </summary>
        private static string Normalise(string value)
        {
            var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return collapsed.ToLowerInvariant();
        }
    }
}
