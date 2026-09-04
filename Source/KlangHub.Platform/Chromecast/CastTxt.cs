using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KlangHub.Application
{
    /// <summary>
    /// The mDNS TXT record a Cast device announces itself with - the richest description of a speaker that
    /// exists without going through Google's cloud. <see cref="DiscoveredDevice.Headers"/> stores it as the
    /// serialized string array the announcement carried; this reads it back into its key/value pairs.
    ///
    /// Measured on the four devices here (2026-09-04), every one of them announces the same keys:
    ///   id  device uuid            cd  a rotating key, not an identity
    ///   md  MODEL      "Q995GD", "Google Home Speaker", "Enchant Speaker", "Smart TV Pro"
    ///   fn  friendly name (what the owner called it in the Home app)
    ///   ca  capability bitmask     ve  cast protocol version    ic  icon path (a generic Google image)
    ///   rs  receiver status text ("Casting: KlangHub")          st  0 idle / 1 an app is running
    ///   bs  hotspot bssid          nf/rm/ct  internal
    /// The model is announced here and NOWHERE else for speakers: eureka_info returns no device_info block on
    /// three of the four devices, and DIAL (:8008/ssdp/device-desc.xml) answers only on the television. So
    /// "md" is the model, full stop - there is no hidden "Harman Kardon" to find.
    /// </summary>
    public static class CastTxt
    {
        public static IReadOnlyDictionary<string, string> Parse(string? headers)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(headers))
                return map;

            foreach (var entry in Entries(headers!))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = entry[..eq].Trim();
                if (key.Length != 0 && !map.ContainsKey(key))
                    map[key] = entry[(eq + 1)..].Trim();
            }

            return map;
        }

        /// <summary>The record is normally a serialized JSON array; older stored profiles and the group
        /// identifier are a bare string. Accept both rather than losing the whole record over its shape.</summary>
        private static IEnumerable<string> Entries(string headers)
        {
            var trimmed = headers.TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                string[]? parsed = null;
                try { parsed = JsonSerializer.Deserialize<string[]>(trimmed); }
                catch (JsonException) { }
                if (parsed != null)
                    return parsed;
            }

            return headers.Split(new[] { '"', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string? Value(string? headers, string key)
            => Parse(headers).TryGetValue(key, out var v) && v.Length != 0 ? v : null;
    }
}
