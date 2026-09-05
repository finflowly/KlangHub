using System;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// What Windows' now-playing session reported, copied out of WinRT into something Core can reason about
    /// and tests can construct. The WinRT side stays a handful of property reads in Platform; every decision
    /// about what those values mean lives here, where it can be proven.
    /// </summary>
    public sealed class SmtcReading
    {
        public string? Title { get; init; }

        public string? Artist { get; init; }

        public string? AlbumTitle { get; init; }

        /// <summary>Some players fill only this in and leave the track artist blank.</summary>
        public string? AlbumArtist { get; init; }

        public TimeSpan? Duration { get; init; }

        /// <summary>
        /// Straightens out what the player left behind. Two repairs are made, both because the alternative
        /// is a raw string on a four-metre screen; nothing beyond them is invented.
        /// </summary>
        public NowPlayingTrack ToTrack()
        {
            var title = Trimmed(Title);
            var artist = Trimmed(Artist) ?? Trimmed(AlbumArtist);

            // Repair 1: a file name dropped into the title field. Reuse the guesser rather than write a
            // second, different opinion about what "03 Teardrop.flac" means.
            if (title != null && LooksLikeAFileName(title))
            {
                var guessed = FileNameGuess.Parse(title);
                title = guessed.Title ?? title;
                artist ??= guessed.Artist;
            }

            // Repair 2: both fields crammed into the title. Only when the player told us no artist -
            // otherwise "Sunday Bloody Sunday - Live" would lose half of itself.
            if (artist == null && title != null)
            {
                var split = NowPlayingText.Parse(title);
                if (split.Artist != null)
                {
                    artist = split.Artist;
                    title = split.Title;
                }
            }

            return new NowPlayingTrack
            {
                Title = title,
                Artist = artist,
                Album = Trimmed(AlbumTitle),
                // Zero is what a live stream reports. Passing it on would draw a finished progress line
                // under a piece that has no end.
                Duration = Duration > TimeSpan.Zero ? Duration : null
            };
        }

        private static bool LooksLikeAFileName(string value)
        {
            var dot = value.LastIndexOf('.');
            if (dot <= 0 || dot == value.Length - 1)
                return false;

            var extension = value[(dot + 1)..].ToLowerInvariant();
            return extension is "mp3" or "flac" or "m4a" or "aac" or "ogg" or "opus" or "wav" or "wma" or "aiff" or "ape";
        }

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
