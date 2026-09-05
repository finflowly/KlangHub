using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// What a file path alone can be made to say. The weakest source there is, and it knows it: everything
    /// it returns arrives in the cascade as <see cref="MetadataSource.FileName"/> and loses to anything
    /// better. Its real value is the path itself, which is the key to the tags and the cover.
    /// <para>
    /// Deliberately incurious. Where the shape is not unmistakable it returns nothing rather than an
    /// invented artist - a wrong name held steadily on a four-metre screen is worse than a missing one.
    /// </para>
    /// </summary>
    public static class FileNameGuess
    {
        private const string Separator = " - ";

        /// <summary>Leading track numbers: "03 ", "03 - ", "03. ", "03_".</summary>
        private static readonly Regex LeadingTrackNumber = new(@"^\d{1,3}\s*[-._]?\s+", RegexOptions.Compiled);

        public static NowPlayingTrack Parse(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new NowPlayingTrack();

            var fileName = NameWithoutExtension(path);
            var folder = FolderName(path);

            string? title = null, artist = null, album = null;

            if (fileName != null)
            {
                var readable = Readable(fileName);
                var separator = readable.IndexOf(Separator, StringComparison.Ordinal);
                if (separator > 0)
                {
                    artist = Meaningful(StripTrackNumber(readable[..separator]));
                    title = Meaningful(readable[(separator + Separator.Length)..]);
                }
                else
                {
                    title = Meaningful(StripTrackNumber(readable));
                }
            }

            if (folder != null)
            {
                var readable = Readable(folder);
                var separator = readable.IndexOf(Separator, StringComparison.Ordinal);
                if (separator > 0)
                {
                    // The file is closer to the track than the folder is, so it keeps the artist it found.
                    artist ??= Meaningful(readable[..separator]);
                    album = Meaningful(readable[(separator + Separator.Length)..]);
                }
            }

            return new NowPlayingTrack { Title = title, Artist = artist, Album = album, FilePath = path.Trim() };
        }

        private static string? NameWithoutExtension(string path)
        {
            var name = LastSegment(path);
            if (name == null)
                return null;

            var dot = name.LastIndexOf('.');
            return dot > 0 ? name[..dot] : name;
        }

        private static string? FolderName(string path)
        {
            var trimmed = path.Trim().TrimEnd('\\', '/');
            var lastSeparator = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            return lastSeparator <= 0 ? null : LastSegment(trimmed[..lastSeparator]);
        }

        private static string? LastSegment(string path)
        {
            var trimmed = path.Trim().TrimEnd('\\', '/');
            if (trimmed.Length == 0)
                return null;

            var lastSeparator = trimmed.LastIndexOfAny(new[] { '\\', '/' });
            var segment = lastSeparator < 0 ? trimmed : trimmed[(lastSeparator + 1)..];
            return segment.Length == 0 ? null : segment;
        }

        /// <summary>Underscores stand in for spaces in half the music collections in the world.</summary>
        private static string Readable(string value) =>
            string.Join(' ', value.Replace('_', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string StripTrackNumber(string value) => LeadingTrackNumber.Replace(value.Trim(), string.Empty);

        /// <summary>A blank, or a bare number left over once the track number is gone, says nothing.</summary>
        private static string? Meaningful(string? value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.All(char.IsDigit))
                return null;

            return trimmed;
        }
    }
}
