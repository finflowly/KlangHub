using KlangHub.Application;
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
    }
}
