using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace KlangHub.Discover
{
    /// <summary>
    /// Recovers a usable IPv4 for a device that announces its _googlecast service IPv6-only in a given scan.
    /// Some Chromecast-built-in speakers (e.g. the Harman Kardon Enchant) flap: sometimes they advertise their
    /// IPv4, sometimes only their IPv6 (a ULA with a %zone). But the IPv4 is knowable - the device self-
    /// advertised it earlier (keyed by the mDNS id=), or the dual-stack multizone group it HOSTS carries the
    /// same IPv6 host together with the IPv4. This cache learns both correlations so the IPv6-only announcement
    /// can be mapped back to an IPv4 and reached over IPv4 end-to-end (no risky IPv6 eureka/TLS path needed).
    /// Keys are scope-normalized (%zone stripped) so the same host matches across announcements/interfaces.
    /// </summary>
    internal sealed class Ipv4Recovery
    {
        private readonly ConcurrentDictionary<string, string> idToIpv4 = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> ipv6ToIpv4 = new ConcurrentDictionary<string, string>();

        /// <summary>Learn this device's IPv4, keyed by its id= and by each IPv6 host announced alongside it.</summary>
        public void Record(string? id, string? ipv4, IEnumerable<IPAddress> addresses)
        {
            if (string.IsNullOrEmpty(ipv4))
                return;

            if (!string.IsNullOrEmpty(id))
                idToIpv4[id!] = ipv4!;

            if (addresses != null)
                foreach (var a in addresses)
                    if (a != null && a.AddressFamily == AddressFamily.InterNetworkV6)
                        ipv6ToIpv4[Normalize(a.ToString())] = ipv4!;
        }

        /// <summary>Recover an IPv4 for an IPv6-only announcement: by id= first (device self-advertised IPv4
        /// earlier), then by the shared IPv6 host (donated by the dual-stack group it hosts). Null if unknown.</summary>
        public string? Recover(string? id, string? ipv6, out string source)
        {
            if (!string.IsNullOrEmpty(id) && idToIpv4.TryGetValue(id!, out var byId))
            {
                source = "id";
                return byId;
            }
            if (!string.IsNullOrEmpty(ipv6) && ipv6ToIpv4.TryGetValue(Normalize(ipv6!), out var byHost))
            {
                source = "group";
                return byHost;
            }
            source = string.Empty;
            return null;
        }

        /// <summary>Strip an IPv6 zone/scope id (the "%8" in fd1a:...%8) so the same host compares equal across
        /// announcements. IPv4 and unscoped literals are returned unchanged.</summary>
        public static string Normalize(string? address)
        {
            if (string.IsNullOrEmpty(address))
                return address ?? string.Empty;
            var pct = address!.IndexOf('%');
            return pct >= 0 ? address.Substring(0, pct) : address;
        }
    }
}
