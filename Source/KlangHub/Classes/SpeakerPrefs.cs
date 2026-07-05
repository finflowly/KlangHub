using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KlangHub.Classes
{
    /// <summary>
    /// Per-speaker UI preferences that outlive a session, keyed by the stable device id. Currently the
    /// per-speaker maximum volume (a hard cap - e.g. the bathroom speaker never above 23%). Persisted to a
    /// small JSON file so it survives restarts without touching the main settings store.
    /// </summary>
    public static class SpeakerPrefs
    {
        private sealed class Entry { public int MaxVolume { get; set; } = 100; }

        private static readonly object gate = new object();
        private static Dictionary<string, Entry> map = Load();

        private static string PathFor()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KlangHub");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "speakers.json");
        }

        private static Dictionary<string, Entry> Load()
        {
            try
            {
                var path = PathFor();
                if (!File.Exists(path))
                    return new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                var json = File.ReadAllText(path);
                var d = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json);
                return d != null ? new Dictionary<string, Entry>(d, StringComparer.OrdinalIgnoreCase)
                                 : new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Save()
        {
            try { File.WriteAllText(PathFor(), JsonSerializer.Serialize(map)); }
            catch (Exception) { /* preferences are best-effort */ }
        }

        /// <summary>The hard volume cap for a speaker (1..100). 100 = no cap. Unknown id -> 100.</summary>
        public static int GetMaxVolume(string? id)
        {
            if (string.IsNullOrEmpty(id))
                return 100;
            lock (gate)
                return map.TryGetValue(id!, out var e) ? Math.Clamp(e.MaxVolume, 1, 100) : 100;
        }

        public static void SetMaxVolume(string? id, int maxVolume)
        {
            if (string.IsNullOrEmpty(id))
                return;
            maxVolume = Math.Clamp(maxVolume, 1, 100);
            lock (gate)
            {
                if (!map.TryGetValue(id!, out var e)) { e = new Entry(); map[id!] = e; }
                e.MaxVolume = maxVolume;
                Save();
            }
        }
    }
}
