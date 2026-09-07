using System;
using System.Globalization;
using System.Text;

namespace KlangHub.Core.NowPlaying
{
    public static class CoverMatch
    {
        public static bool Same(string? one, string? other)
        {
            var a = Normalise(one);
            var b = Normalise(other);

            if (a.Length == 0 || b.Length == 0)
                return false;

            return string.Equals(a, b, StringComparison.Ordinal) ||
                   string.Equals(Tight(a), Tight(b), StringComparison.Ordinal);
        }

        private static string Tight(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal);

        public static string Normalise(string? value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (text.Length == 0)
                return string.Empty;

            text = WithoutBrackets(text);
            text = WithoutGuests(text);

            var folded = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(folded.Length);
            var space = false;

            foreach (var c in folded)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(c))
                {
                    if (space && builder.Length > 0)
                        builder.Append(' ');

                    space = false;
                    builder.Append(c);
                }
                else
                {
                    space = true;
                }
            }

            return builder.ToString();
        }

        private static string WithoutBrackets(string text)
        {
            var builder = new StringBuilder(text.Length);
            var depth = 0;

            foreach (var c in text)
            {
                if (c is '(' or '[')
                {
                    depth++;
                    continue;
                }

                if (c is ')' or ']')
                {
                    if (depth > 0)
                        depth--;
                    continue;
                }

                if (depth == 0)
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static readonly string[] Guests = { " feat. ", " feat ", " featuring ", " ft. ", " ft ", " with " };

        private static string WithoutGuests(string text)
        {
            foreach (var guest in Guests)
            {
                var at = text.IndexOf(guest, StringComparison.Ordinal);
                if (at > 0)
                    text = text[..at];
            }

            return text;
        }
    }
}
