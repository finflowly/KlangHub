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
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("recordings", out var recordings) ||
                    recordings.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var recording in recordings.EnumerateArray())
                {
                    if (!CoverMatch.Same(Text(recording, "title"), title))
                        continue;

                    if (!CoverMatch.Same(CreditedArtist(recording), artist))
                        continue;

                    var release = BestRelease(recording);
                    if (release != null)
                        return release;
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
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

        private static string? BestRelease(JsonElement recording)
        {
            if (!recording.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Array)
                return null;

            string? fallback = null;
            string? official = null;

            foreach (var release in releases.EnumerateArray())
            {
                var id = Text(release, "id");
                if (id.Length == 0)
                    continue;

                fallback ??= id;

                if (!string.Equals(Text(release, "status"), "Official", StringComparison.OrdinalIgnoreCase))
                    continue;

                official ??= id;

                if (release.TryGetProperty("release-group", out var group) &&
                    string.Equals(Text(group, "primary-type"), "Album", StringComparison.OrdinalIgnoreCase))
                    return id;
            }

            return official ?? fallback;
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
