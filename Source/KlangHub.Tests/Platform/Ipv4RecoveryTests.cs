using System.Collections.Generic;
using System.Net;
using KlangHub.Discover;
using Xunit;

namespace KlangHub.Tests.Platform
{
    // Locks IPv4-recovery for a Chromecast device that announces its _googlecast IPv6-only in a scan (the
    // Harman Kardon Enchant): recover its IPv4 from a prior self-advertised IPv4 (by id=) or from the dual-
    // stack "the multi-room group" group it hosts (the shared IPv6 host donates the IPv4), so it stays reachable
    // over IPv4 without any IPv6 eureka/TLS path.
    public class Ipv4RecoveryTests
    {
        private const string EnchantIpv6 = "fd1a:89fe:f83b:3dad:81cd:c71c:3d0e:b4c4";

        private static IEnumerable<IPAddress> Addrs(params string[] addrs)
        {
            var list = new List<IPAddress>();
            foreach (var a in addrs) list.Add(IPAddress.Parse(a));
            return list;
        }

        [Fact]
        public void Recovers_ipv4_by_device_id_when_it_self_advertised_ipv4_earlier()
        {
            var r = new Ipv4Recovery();
            r.Record("enchant-id", "192.168.1.154", Addrs("192.168.1.154", EnchantIpv6));

            var ip = r.Recover("enchant-id", EnchantIpv6 + "%8", out var source);

            Assert.Equal("192.168.1.154", ip);
            Assert.Equal("id", source);
        }

        [Fact]
        public void Recovers_ipv4_from_the_hosted_group_via_shared_ipv6_host()
        {
            var r = new Ipv4Recovery();
            // The "the multi-room group" group the Enchant hosts announces IPv4 + the same IPv6 host.
            r.Record("group-id", "192.168.1.154", Addrs("192.168.1.154", EnchantIpv6));

            // The Enchant's own IPv6-only announcement: different id, same IPv6 host, carrying a %zone.
            var ip = r.Recover("enchant-id", EnchantIpv6 + "%8", out var source);

            Assert.Equal("192.168.1.154", ip);
            Assert.Equal("group", source);
        }

        [Fact]
        public void Zone_id_is_ignored_when_matching_the_ipv6_host()
        {
            var r = new Ipv4Recovery();
            r.Record(null, "192.168.1.154", Addrs("fd1a::1%8"));   // learned with zone %8

            // looked up with a different zone %5 - still matches (scope normalized).
            Assert.Equal("192.168.1.154", r.Recover(null, "fd1a::1%5", out var source));
            Assert.Equal("group", source);
        }

        [Fact]
        public void Returns_null_on_a_miss()
        {
            var r = new Ipv4Recovery();
            r.Record("known", "192.168.1.50", Addrs("192.168.1.50"));

            Assert.Null(r.Recover("unknown", "fd1a::99%8", out var source));
            Assert.Equal(string.Empty, source);
        }

        [Fact]
        public void Record_learns_nothing_from_an_ipv6_only_announcement()
        {
            var r = new Ipv4Recovery();
            r.Record("enchant-id", null, Addrs("fd1a::1%8"));   // no IPv4 to learn

            Assert.Null(r.Recover("enchant-id", "fd1a::1%8", out _));
        }

        [Theory]
        [InlineData("fd1a::1%8", "fd1a::1")]
        [InlineData("fd1a::1", "fd1a::1")]
        [InlineData("192.168.1.154", "192.168.1.154")]
        public void Normalize_strips_the_zone_id(string input, string expected)
        {
            Assert.Equal(expected, Ipv4Recovery.Normalize(input));
        }
    }
}
