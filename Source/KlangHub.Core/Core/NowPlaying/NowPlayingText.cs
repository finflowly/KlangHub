using System;
using System.Collections.Generic;
using System.Linq;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// Reads the now-playing text file a player or a small helper keeps up to date.
    /// <para>
    /// There is no standard for this file. VLC writes a template the user composed, scrobbler helpers write
    /// labelled lines, and plenty of people just write "Artist - Title". So this recognises the shapes that
    /// are actually in use and refuses to guess for anything else: an unrecognised line yields nothing,
    /// because "$artist" or a half-written line on a four-metre screen is worse than an empty one.
    /// </para>
    /// </summary>
    public static class NowPlayingText
    {
        /// <summary>" - " with spaces. A bare hyphen belongs to names like "Jay-Z".</summary>
        private const string ArtistTitleSeparator = " - ";

        public static NowPlayingTrack Parse(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new NowPlayingTrack();

            var lines = content
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            if (lines.Count == 0)
                return new NowPlayingTrack();

            var labelled = ParseLabelled(lines);
            if (labelled != null)
                return labelled;

            return lines.Count == 1 ? ParseSingleLine(lines[0]) : ParsePlainLines(lines);
        }

        /// <summary>
        /// "artist: X", "ARTIST=X" and VLC's "$artist: X" all mean the same thing. Returns null when the
        /// text is not labelled at all, so the plainer shapes get their turn.
        /// </summary>
        private static NowPlayingTrack? ParseLabelled(IEnumerable<string> lines)
        {
            string? title = null, artist = null, album = null, file = null;
            var recognised = false;

            foreach (var line in lines)
            {
                var separator = line.IndexOfAny(new[] { ':', '=' });
                if (separator <= 0)
                    continue;

                var label = line[..separator].Trim().TrimStart('$').ToLowerInvariant();
                var value = Clean(line[(separator + 1)..]);
                if (value == null)
                    continue;

                switch (label)
                {
                    case "artist": artist = value; recognised = true; break;
                    case "title": title = value; recognised = true; break;
                    case "album": album = value; recognised = true; break;
                    case "file": file = value; recognised = true; break;
                }
            }

            if (!recognised)
                return null;

            return new NowPlayingTrack { Title = title, Artist = artist, Album = album, FilePath = file };
        }

        private static NowPlayingTrack ParseSingleLine(string line)
        {
            var separator = line.IndexOf(ArtistTitleSeparator, StringComparison.Ordinal);
            if (separator < 0)
                return new NowPlayingTrack { Title = Clean(line) };

            // The FIRST separator only: "U2 - Sunday Bloody Sunday - Live" keeps its second half.
            return new NowPlayingTrack
            {
                Artist = Clean(line[..separator]),
                Title = Clean(line[(separator + ArtistTitleSeparator.Length)..])
            };
        }

        /// <summary>Artist, title, album - the order the prompt names, and the order helpers write.</summary>
        private static NowPlayingTrack ParsePlainLines(IReadOnlyList<string> lines) => new()
        {
            Artist = Clean(lines[0]),
            Title = Clean(lines[1]),
            Album = lines.Count > 2 ? Clean(lines[2]) : null
        };

        /// <summary>
        /// Trims, and treats a placeholder that was never filled in as nothing at all. A helper that fired
        /// before the player told it anything leaves "$title" standing, and that must not reach the screen.
        /// </summary>
        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('$'))
                return null;

            return trimmed;
        }
    }
}
