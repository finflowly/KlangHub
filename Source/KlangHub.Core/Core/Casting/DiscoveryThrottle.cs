using System;
using System.Collections.Generic;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Stops the same announcement from being acted on over and over.
    ///
    /// One mDNS scan does not produce one announcement per device: the browser reports every service as
    /// "added" and again as "changed", once per network interface. In the maintainer's log of 2026-09-05 a single
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
        private readonly Dictionary<string, (DateTime When, string Fingerprint)> seen = new(StringComparer.OrdinalIgnoreCase);

        public DiscoveryThrottle(TimeSpan? quietPeriod = null)
            => this.quietPeriod = quietPeriod ?? DefaultQuietPeriod;

        /// <param name="key">What identifies the device across announcements - its mDNS id, or its endpoint
        /// when it announces none.</param>
        /// <param name="fingerprint">Everything that would make this announcement worth acting on again:
        /// address, port, TXT record.</param>
        public bool ShouldAct(string? key, string? fingerprint, DateTime now)
        {
            if (string.IsNullOrEmpty(key))
                return true;   // nothing to recognise it by - never swallow it

            lock (sync)
            {
                if (seen.TryGetValue(key!, out var last)
                    && string.Equals(last.Fingerprint, fingerprint ?? string.Empty, StringComparison.Ordinal)
                    && now - last.When < quietPeriod)
                    return false;

                seen[key!] = (now, fingerprint ?? string.Empty);
                return true;
            }
        }

        public void Forget(string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (sync) { seen.Remove(key!); }
        }
    }
}
