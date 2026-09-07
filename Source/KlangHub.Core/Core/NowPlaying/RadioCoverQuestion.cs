using System;

namespace KlangHub.Core.NowPlaying
{
    public readonly record struct CoverQuestion(bool Worth, string Artist, string Title, string Key)
    {
        public static readonly CoverQuestion None = new(false, string.Empty, string.Empty, string.Empty);
    }

    public static class RadioCoverQuestion
    {
        public static CoverQuestion For(NowPlayingTrack? track, bool hasCoverAlready)
        {
            if (track == null || hasCoverAlready)
                return CoverQuestion.None;

            if (!string.IsNullOrWhiteSpace(track.FilePath))
                return CoverQuestion.None;

            var artist = (track.Artist ?? string.Empty).Trim();
            var title = (track.Title ?? string.Empty).Trim();

            if (artist.Length == 0 || title.Length == 0)
                return CoverQuestion.None;

            if (LooksLikeAnAddress(artist) || LooksLikeAnAddress(title))
                return CoverQuestion.None;

            var key = CoverMatch.Normalise(artist) + "|" + CoverMatch.Normalise(title);
            if (key.Length < 3)
                return CoverQuestion.None;

            return new CoverQuestion(true, artist, title, key);
        }

        private static bool LooksLikeAnAddress(string value) =>
            value.Contains("://", StringComparison.Ordinal) ||
            value.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }
}
