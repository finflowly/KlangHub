using System;

namespace KlangHub.Core.NowPlaying
{
    public static class StageCover
    {
        public static string? Url(string? artworkUrl, string? coverFingerprint, string? onlineUrl)
        {
            if (!string.IsNullOrWhiteSpace(coverFingerprint))
                return string.IsNullOrWhiteSpace(artworkUrl) ? null : artworkUrl;

            if (string.IsNullOrWhiteSpace(onlineUrl))
                return null;

            var trimmed = onlineUrl.Trim();
            return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : null;
        }
    }
}
