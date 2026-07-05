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
        [InlineData("AA:BB:CC:DD:EE:FF", true)]   // real MAC (the Samsung Soundbar)
        [InlineData("00:00:00:00:00:00", false)]  // all-zeros placeholder (Google TV / TCL TV / Enchant)
        [InlineData("", false)]
        [InlineData(null, false)]
        public void HasRealMac_rejects_the_all_zeros_placeholder(string? mac, bool expected)
        {
            Assert.Equal(expected, Devices.HasRealMac(mac));
        }

        // Locks the fix for the Enchant-hosts-its-own-group case: the Enchant (placeholder MAC) at .154:8009
        // fronts the "the multi-room group" group at .154:32223. IP-only dedup collapsed the speaker into the group so
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
    }
}
