using KlangHub.Application;
using KlangHub.Discover;
using Xunit;

namespace KlangHub.Tests.Application
{
    public class DevicesTests
    {
        // Locks the fix for the collapsed-tiles / misrouted-cast bug: Google TV, TCL TV and the Enchant all
        // report the placeholder eureka MAC 00:00:00:00:00:00, which must NOT be used as a dedup identity
        // (else they merge into one tile). Only a real MAC counts; everything else falls back to IP dedup.
        [Theory]
        [InlineData("A4:B1:C2:D3:E4:F5", true)]   // a MAC-shaped id. Deliberately made up: a real one identifies a particular piece of
                                                  // hardware in somebody's home, and this repository is public.
        [InlineData("00:00:00:00:00:00", false)]  // all-zeros placeholder (Google TV / TCL TV / Enchant)
        [InlineData("", false)]
        [InlineData(null, false)]
        public void HasRealMac_rejects_the_all_zeros_placeholder(string? mac, bool expected)
        {
            Assert.Equal(expected, Devices.HasRealMac(mac));
        }

        // Locks the fix for the Enchant-hosts-its-own-group case: the Enchant (placeholder MAC) at .154:8009
        // fronts the multi-room group at .154:32223. IP-only dedup collapsed the speaker into the group so
        // it never got a tile; dedup must be by IP:port (matching ChromecastDeviceId.From).
        [Fact]
        public void Placeholder_speaker_and_the_group_it_hosts_are_distinct_by_ip_port()
        {
            var speaker = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009 };
            var group = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 32223 };

            Assert.False(Devices.SamePlaceholderEndpoint(group, speaker));
            Assert.False(Devices.SamePlaceholderEndpoint(speaker, group));
        }

        [Fact]
        public void Placeholder_device_matches_itself_by_ip_port()
        {
            var a = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009 };
            var b = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009 };

            Assert.True(Devices.SamePlaceholderEndpoint(a, b));
        }

        [Theory]
        [InlineData("192.168.1.112", "192.168.1.57")]   // Google TV vs TCL TV - both placeholder MAC, :8009
        [InlineData("192.168.1.154", "192.168.1.201")]  // distinct IPs must never merge
        public void Placeholder_devices_at_different_ips_are_distinct(string ipA, string ipB)
        {
            var a = new DiscoveredDevice { IPAddress = ipA, Port = 8009 };
            var b = new DiscoveredDevice { IPAddress = ipB, Port = 8009 };

            Assert.False(Devices.SamePlaceholderEndpoint(a, b));
        }

        [Fact]
        public void SamePlaceholderEndpoint_is_false_for_null()
        {
            var a = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009 };

            Assert.False(Devices.SamePlaceholderEndpoint(null, a));
            Assert.False(Devices.SamePlaceholderEndpoint(a, null));
            Assert.False(Devices.SamePlaceholderEndpoint(null, null));
        }

        // Locks the fix for the DHCP-move zombie tile: the Enchant moved 192.168.1.154 -> .156 (its IPv4 AND its
        // fd1a IPv6 host both changed); only the mDNS id= is stable. Reconcile a placeholder-MAC device to its
        // existing tile by that stable id BEFORE the IP:port fallback, so the move updates the tile instead of
        // spawning a second one + orphaning the old (which then hammers reconnect forever).
        [Fact]
        public void Same_stable_id_at_a_new_endpoint_is_the_same_device()
        {
            var oldEntry = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009, Id = "enchant-uuid" };
            var moved = new DiscoveredDevice { IPAddress = "192.168.1.156", Port = 8009, Id = "enchant-uuid" };

            Assert.True(Devices.SameStableId(oldEntry, moved));
        }

        [Fact]
        public void Different_stable_ids_are_different_devices()
        {
            // Google TV vs TCL TV: both placeholder MAC, distinct mDNS ids -> must never merge.
            var googleTv = new DiscoveredDevice { IPAddress = "192.168.1.112", Port = 8009, Id = "googletv-uuid" };
            var tclTv = new DiscoveredDevice { IPAddress = "192.168.1.57", Port = 8009, Id = "tcltv-uuid" };

            Assert.False(Devices.SameStableId(googleTv, tclTv));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void No_incoming_id_never_matches_by_id_so_it_falls_back_to_ip_port(string? incomingId)
        {
            // A device that advertises no id= must not id-match anything - GetDevice then uses IP:port as today.
            var existing = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009, Id = "some-uuid" };
            var incoming = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009, Id = incomingId! };

            Assert.False(Devices.SameStableId(existing, incoming));
        }

        [Fact]
        public void SameStableId_is_false_for_null()
        {
            var a = new DiscoveredDevice { IPAddress = "192.168.1.154", Port = 8009, Id = "x" };

            Assert.False(Devices.SameStableId(null, a));
            Assert.False(Devices.SameStableId(a, null));
            Assert.False(Devices.SameStableId(null, null));
        }
    }
}
