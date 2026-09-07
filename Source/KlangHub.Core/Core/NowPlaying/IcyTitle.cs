using System;

namespace KlangHub.Core.NowPlaying
{
    public readonly record struct IcySplit(bool Certain, string? Artist, string? Title);

    public static class IcyTitle
    {
        private const string Separator = " - ";

        public static IcySplit Split(string? line)
        {
            var text = (line ?? string.Empty).Trim();
            if (text.Length == 0)
                return new IcySplit(false, null, null);

            var first = text.IndexOf(Separator, StringComparison.Ordinal);
            if (first < 0)
                return new IcySplit(false, null, text);

            if (text.IndexOf(Separator, first + Separator.Length, StringComparison.Ordinal) >= 0)
                return new IcySplit(false, null, text);

            var artist = text[..first].Trim();
            var title = text[(first + Separator.Length)..].Trim();

            if (artist.Length == 0 || title.Length == 0)
                return new IcySplit(false, null, text);

            return new IcySplit(true, artist, title);
        }
    }
}
