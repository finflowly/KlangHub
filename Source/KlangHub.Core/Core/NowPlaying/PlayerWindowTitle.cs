using System;
using System.Collections.Generic;
using System.Linq;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// Reads what a music player writes in its own title bar.
    /// <para>
    /// Why this exists: measured on 2026-09-05, Clementine playing a track reports <b>nothing</b> to
    /// Windows' now-playing session - <c>GetSessions()</c> returns zero - while its title bar reads
    /// "Aquanote - Nowhere (Speakeasy remix)". For everyone casting from a player like that, this is the
    /// difference between having to set up a helper script and the television simply knowing what is on.
    /// </para>
    /// <para>
    /// It is still a guess, and it ranks accordingly: above a file name, below a now-playing file the user
    /// deliberately set up. The discipline is in what it refuses - an idle player's title bar says
    /// "Clementine 1.4.0rc1", and that must never reach a television dressed up as a song.
    /// </para>
    /// </summary>
    public static class PlayerWindowTitle
    {
        /// <summary>
        /// Programme names players hang on their own title bar. Matched as a whole trailing or leading
        /// segment only - a band called "Clementine" keeps its name.
        /// </summary>
        private static readonly string[] ProgrammeNames =
        {
            "clementine", "vlc media player", "vlc", "foobar2000", "aimp", "musicbee", "winamp",
            "deadbeef", "mpc-hc", "mpc-be", "audacious", "quod libet", "strawberry", "media player",
            "windows media player", "groove music", "itunes", "spotify"
        };

        /// <summary>State markers some players put in front of the track.</summary>
        private static readonly string[] StateMarkers = { "[paused]", "[playing]", "[stopped]", "(paused)" };

        public static NowPlayingTrack Parse(string? processName, string? windowTitle)
        {
            var title = (windowTitle ?? string.Empty).Trim();
            if (title.Length == 0)
                return new NowPlayingTrack();

            title = StripStateMarker(title);
            title = StripProgrammeName(title);

            if (title.Length == 0 || IsOnlyProgrammeName(title))
                return new NowPlayingTrack();

            // The shapes in a title bar are the same ones a now-playing file uses, so the same reader
            // handles them - one opinion about what "Artist - Title" means, not two.
            return NowPlayingText.Parse(title);
        }

        private static string StripStateMarker(string title)
        {
            foreach (var marker in StateMarkers)
            {
                if (title.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                    return title[marker.Length..].Trim();
            }

            return title;
        }

        /// <summary>
        /// Removes a trailing " - Clementine" or " - VLC media player". Only ever the LAST segment, and only
        /// when that whole segment is a programme name: "U2 - Sunday Bloody Sunday - Live" keeps all of it.
        /// </summary>
        private static string StripProgrammeName(string title)
        {
            var separator = title.LastIndexOf(" - ", StringComparison.Ordinal);
            if (separator <= 0)
                return title;

            var tail = title[(separator + 3)..].Trim();
            return IsProgrammeName(tail) ? title[..separator].Trim() : title;
        }

        private static bool IsOnlyProgrammeName(string title)
        {
            var cleaned = title.TrimEnd('-', ' ').Trim();
            return cleaned.Length == 0 || IsProgrammeName(cleaned);
        }

        /// <summary>
        /// True for "Clementine" and for "Clementine 1.4.0rc1": a programme name optionally followed by
        /// something that is only digits and punctuation, which is how every version string looks.
        /// </summary>
        private static bool IsProgrammeName(string value)
        {
            var lower = value.Trim().ToLowerInvariant();
            if (lower.Length == 0)
                return false;

            foreach (var name in ProgrammeNames)
            {
                if (lower == name)
                    return true;

                if (lower.StartsWith(name + " ", StringComparison.Ordinal) &&
                    IsVersionish(lower[(name.Length + 1)..]))
                    return true;
            }

            return false;
        }

        private static bool IsVersionish(string rest) =>
            rest.Length > 0 && rest.All(c => char.IsDigit(c) || c == '.' || c == 'v' || c == 'r' || c == 'c' ||
                                             c == '-' || c == ' ' || c == 'b' || c == 'e' || c == 't' || c == 'a');
    }
}
