using KlangHub.Discover;
using Tmds.MDns;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Since the browsers are held for the life of the object (commit b4ba035, so the garbage collector can
    /// no longer take them mid-search), MdnsSearch must not append a new set on every call. ScanForDevices
    /// runs it per scan and on every IP change, so what used to be an invisible leak - the old browsers were
    /// simply collected - is now four more live browsers per scan, each with a socket on every interface,
    /// all receiving the same multicast traffic for the rest of the session.
    ///
    /// MdnsDiscovery on the other side of the app has guarded against exactly this from the start.
    /// </summary>
    public class DiscoverDevicesBrowserTests
    {
        /// <summary>Creates the browsers as production does, but never opens a socket.</summary>
        private sealed class OfflineDiscoverDevices : DiscoverDevices
        {
            internal override void StartBrowser(ServiceBrowser browser, string serviceType) { }
        }

        [Fact]
        public void MdnsSearch_browses_googlecast_googlezone_airplay_and_raop()
        {
            var discover = new OfflineDiscoverDevices();

            discover.MdnsSearch();

            Assert.Equal(4, discover.BrowserCount);
        }

        [Fact]
        public void MdnsSearch_does_not_stack_browsers_when_a_scan_repeats()
        {
            var discover = new OfflineDiscoverDevices();

            discover.MdnsSearch();
            discover.MdnsSearch();
            discover.MdnsSearch();

            Assert.Equal(4, discover.BrowserCount);
        }
    }
}
