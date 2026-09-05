using System;
using System.Collections.Generic;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Stops the same announcement from being acted on over and over.
    ///
    /// One mDNS scan does not produce one announcement per device: the browser reports every service as
    /// "added" and again as "changed", once per network interface. In a measured log of 2026-09-05 a single
    /// scan produced SIX identical announcements for the soundbar, and each one sent an HTTP request to a
    /// device that was decoding FLAC at the time - repeated several times a minute, for every speaker.
    ///
    /// So a repetition of something already known waits. Anything that actually CHANGED - a new device, a
    /// different address, a different TXT record - goes through immediately, which is what the DHCP-move
    /// reconciliation depends on.
    /// </summary>
    public sealed class DiscoveryThrottle
    {
        public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromSeconds(30);

        private readonly TimeSpan quietPeriod;
        private readonly object sync = new();
        /// <summary>Keyed by device; the time is a monotonic millisecond count, never the wall clock - a
        /// backward clock step (the end of summer time) would otherwise suppress announcements for an hour.</summary>
        private readonly Dictionary<string, (long WhenMs, string Fingerprint)> seen = new(StringComparer.OrdinalIgnoreCase);

        public DiscoveryThrottle(TimeSpan? quietPeriod = null)
            => this.quietPeriod = quietPeriod ?? DefaultQuietPeriod;

        /// <param name="key">What identifies the device across announcements - its mDNS id, or its endpoint
        /// when it announces none.</param>
        /// <param name="fingerprint">Everything that would make this announcement worth acting on again:
        /// address, port, TXT record.</param>
        public bool ShouldAct(string? key, string? fingerprint)
            => ShouldAct(key, fingerprint, Environment.TickCount64);

        /// <summary>Testable overload: <paramref name="nowMs"/> is a monotonic millisecond count.</summary>
        public bool ShouldAct(string? key, string? fingerprint, long nowMs)
        {
            if (string.IsNullOrEmpty(key))
                return true;   // nothing to recognise it by - never swallow it

            lock (sync)
            {
                if (seen.TryGetValue(key!, out var last)
                    && string.Equals(last.Fingerprint, fingerprint ?? string.Empty, StringComparison.Ordinal)
                    && nowMs - last.WhenMs < (long)quietPeriod.TotalMilliseconds)
                    return false;

                seen[key!] = (nowMs, fingerprint ?? string.Empty);
                return true;
            }
        }

        /// <summary>Forgets everything, so the next announcement from any device is acted on. What the user
        /// means by pressing "Scan again": without it the button was a silent no-op for up to the quiet
        /// period, leaving no trace in the very log they are asked to send.</summary>
        public void ForgetAll()
        {
            lock (sync) { seen.Clear(); }
        }

        public void Forget(string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync) { seen.Remove(key!); }
        }
    }
}
