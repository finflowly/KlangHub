using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KlangHub.Core.NowPlaying
{
    public static class MusicBrainzAnswer
    {
        public static string Question(string artist, string title) =>
            "https://musicbrainz.org/ws/2/recording?fmt=json&limit=10&query=" +
            Uri.EscapeDataString("artist:\"" + Quoted(artist) + "\" AND recording:\"" + Quoted(title) + "\"");

        private static string Quoted(string value) => value.Replace("\"", " ", StringComparison.Ordinal).Trim();

        public static string? ReleaseId(string? json, string artist, string title)
        {
            var candidates = ReleaseIds(json, artist, title);
            return candidates.Count == 0 ? null : candidates[0];
        }

        public static IReadOnlyList<string> ReleaseIds(string? json, string artist, string title)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<string>();

            var albums = new List<string>();
            var official = new List<string>();
            var rest = new List<string>();

            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("recordings", out var recordings) ||
                    recordings.ValueKind != JsonValueKind.Array)
                    return Array.Empty<string>();

                foreach (var recording in recordings.EnumerateArray())
                {
                    if (!CoverMatch.Same(Text(recording, "title"), title))
                        continue;

                    if (!CoverMatch.Same(CreditedArtist(recording), artist))
                        continue;

                    Sort(recording, albums, official, rest);
                }
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }

            var ordered = new List<string>(albums.Count + official.Count + rest.Count);
            var already = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in new[] { albums, official, rest })
                foreach (var id in group)
                    if (already.Add(id))
                        ordered.Add(id);

            return ordered;
        }

        private static void Sort(JsonElement recording, List<string> albums, List<string> official, List<string> rest)
        {
            if (!recording.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Array)
                return;

            foreach (var release in releases.EnumerateArray())
            {
                var id = Text(release, "id");
                if (id.Length == 0)
                    continue;

                if (!string.Equals(Text(release, "status"), "Official", StringComparison.OrdinalIgnoreCase))
                {
                    rest.Add(id);
                    continue;
                }

                if (release.TryGetProperty("release-group", out var group) &&
                    string.Equals(Text(group, "primary-type"), "Album", StringComparison.OrdinalIgnoreCase))
                    albums.Add(id);
                else
                    official.Add(id);
            }
        }

        private static string CreditedArtist(JsonElement recording)
        {
            if (!recording.TryGetProperty("artist-credit", out var credits) || credits.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var names = new List<string>();
            foreach (var credit in credits.EnumerateArray())
            {
                var name = Text(credit, "name");
                if (name.Length == 0 && credit.TryGetProperty("artist", out var artist))
                    name = Text(artist, "name");

                if (name.Length > 0)
                    names.Add(name);
            }

            return names.Count == 0 ? string.Empty : names[0];
        }

        private static string Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        public static string FrontCover(string releaseId) =>
            "https://coverartarchive.org/release/" + releaseId + "/front-500";
    }
}
