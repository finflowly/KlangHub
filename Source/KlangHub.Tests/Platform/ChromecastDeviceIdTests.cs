using KlangHub.Discover;
using KlangHub.Platform.Casting.Chromecast;
using Xunit;

namespace KlangHub.Tests.Platform
{
    // Locks the fix for the "3 x Enchant Speaker tiles that light up together" bug: Google TV / TCL TV / the
    // Enchant all report the placeholder MAC 00:00:00:00:00:00, so the device id must fall through to
    // IP:port (a real per-device identity) - otherwise all three share one id and their tiles resolve to the
    // same session.
    public class ChromecastDeviceIdTests
    {
        [Fact]
        public void Placeholder_mac_falls_through_to_ip_port()
        {
            var id = ChromecastDeviceId.From(new DiscoveredDevice
            {
                Usn = null!,
                MACAddress = "00:00:00:00:00:00",
                IPAddress = "192.168.1.112",
                Port = 8009,
            });

            Assert.Equal("192.168.1.112:8009", id);
        }

        [Fact]
        public void Real_mac_is_used_as_the_id()
        {
            var id = ChromecastDeviceId.From(new DiscoveredDevice
            {
                Usn = null!,
                MACAddress = "A4:B1:C2:D3:E4:F5",
                IPAddress = "192.168.1.201",
                Port = 8009,
            });

            Assert.Equal("A4:B1:C2:D3:E4:F5", id);
        }

        [Fact]
        public void Usn_wins_when_present()
        {
            var id = ChromecastDeviceId.From(new DiscoveredDevice
            {
                Usn = "host.local",
                MACAddress = "A4:B1:C2:D3:E4:F5",
            });

            Assert.Equal("host.local", id);
        }
    }
}
