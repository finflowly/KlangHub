using System;

namespace KlangHub.Core.Diagnostics
{
    /// <summary>
    /// Compares the version of a GitHub release tag with the running build.
    ///
    /// It used to be a plain string comparison, which is wrong twice over. A conventional "v1.0.0" tag is
    /// greater than the running "1.0" as text, so the app would have offered the user the build they were
    /// already running, every start, for ever. And "1.10" sorts BELOW "1.9" as text, so the first release
    /// past nine would have gone unannounced.
    /// </summary>
    public static class ReleaseVersion
    {
        /// <summary>Reads a tag like "v1.2.3", "1.2" or "release-1.2.3" as a version, or null.</summary>
        public static Version? Parse(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            // Take the first run of digits and dots - that is the version, whatever decorates it.
            int start = -1, end = -1;
            for (int i = 0; i < tag!.Length; i++)
            {
                bool part = char.IsDigit(tag[i]) || (tag[i] == '.' && start >= 0);
                if (part && start < 0) start = i;
                if (part) end = i;
                else if (start >= 0) break;
            }

            if (start < 0)
                return null;

            var text = tag[start..(end + 1)].Trim('.');
            return Version.TryParse(text.Contains('.') ? text : text + ".0", out var version) ? version : null;
        }

        /// <summary>
        /// True when <paramref name="latestTag"/> really is a later release than <paramref name="current"/>.
        /// Unparseable input answers false: never nag about a release we cannot even read.
        /// </summary>
        public static bool IsNewer(string? latestTag, string? current)
        {
            var latest = Parse(latestTag);
            var running = Parse(current);
            if (latest == null || running == null)
                return false;

            // Compare on the parts that were actually stated: "1.0" and "1.0.0" are the same release, and
            // Version fills the unstated parts with -1, which would otherwise make them differ.
            int parts = Math.Max(Fields(latestTag!), Fields(current!));
            return Normalise(latest, parts) > Normalise(running, parts);
        }

        private static int Fields(string tag) => Math.Clamp(tag.Split('.').Length, 2, 4);

        private static Version Normalise(Version v, int parts) => new(
            v.Major,
            v.Minor < 0 ? 0 : v.Minor,
            parts >= 3 ? Math.Max(v.Build, 0) : 0,
            parts >= 4 ? Math.Max(v.Revision, 0) : 0);
    }
}
