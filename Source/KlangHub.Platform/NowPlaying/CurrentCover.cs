using System;
using System.Security.Cryptography;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// The picture that belongs to what is playing right now, or nothing when no cover was found.
    /// <para>
    /// A single process-wide value, like <see cref="KlangHub.Communication.CastReceiver"/>: every device
    /// connection serves the same picture, and it changes when the music does. The fingerprint is what makes
    /// a receiver actually fetch a new one - they cache by URL, so without a changing address the first
    /// cover of the evening would stay on screen for the whole album.
    /// </para>
    /// </summary>
    public static class CurrentCover
    {
        private static byte[]? bytes;
        private static string fingerprint = string.Empty;
        private static readonly object gate = new();

        /// <summary>The cover to serve, or null to fall back to the branded artwork.</summary>
        public static byte[]? Bytes
        {
            get { lock (gate) return bytes; }
        }

        /// <summary>Short, stable identifier of the current picture; empty when there is none.</summary>
        public static string Fingerprint
        {
            get { lock (gate) return fingerprint; }
        }

        public static void Set(byte[]? cover)
        {
            lock (gate)
            {
                if (cover == null || cover.Length == 0)
                {
                    bytes = null;
                    fingerprint = string.Empty;
                    return;
                }

                bytes = cover;
                fingerprint = Convert.ToHexString(SHA256.HashData(cover), 0, 6).ToLowerInvariant();
            }
        }

        public static void Clear() => Set(null);
    }
}
