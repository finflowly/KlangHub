using System.Net;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Classes
{
    /// <summary>
    /// Which addresses count as "the local network".
    /// <para>
    /// This decides which adapter KlangHub streams from, so getting it wrong means offering the
    /// speakers an address they cannot reach. The private ranges are the ones RFC 1918 sets aside, and
    /// the middle one is not a whole first octet: it is 172.16 through 172.31, not all of 172. A plain
    /// <c>StartsWith("172.")</c> also accepts 172.0 and 172.217 - the latter being ordinary public
    /// space that Google, among others, actually uses.
    /// </para>
    /// </summary>
    public class NetworkLocalRangeTests
    {
        [Theory]
        [InlineData("192.168.1.20")]
        [InlineData("192.168.255.255")]
        [InlineData("10.0.0.1")]
        [InlineData("10.255.255.254")]
        [InlineData("172.16.0.1")]   // first address of the RFC 1918 middle block
        [InlineData("172.31.255.254")] // last address of it
        [InlineData("172.20.10.1")]
        public void An_address_from_a_private_range_is_local(string address)
            => Assert.True(Network.IsInLocalIpRange(IPAddress.Parse(address)));

        [Theory]
        [InlineData("172.15.0.1")]   // one below the block
        [InlineData("172.32.0.1")]   // one above it
        [InlineData("172.0.0.1")]
        [InlineData("172.217.16.14")] // public space, and reachable - the kind of address the old check took
        [InlineData("8.8.8.8")]
        [InlineData("100.64.0.1")]   // carrier-grade NAT, not a home network
        [InlineData("169.254.10.5")] // link-local: an adapter that failed to get a lease
        public void An_address_outside_them_is_not(string address)
            => Assert.False(Network.IsInLocalIpRange(IPAddress.Parse(address)));

        [Fact]
        public void A_string_that_merely_starts_the_same_way_is_not_local()
        {
            // The check compared text, so 192.168… never arose - but 10.x did its work by prefix, and
            // comparing addresses as numbers is what removes the whole class of question.
            Assert.False(Network.IsInLocalIpRange(IPAddress.Parse("110.0.0.1")));
            Assert.False(Network.IsInLocalIpRange(IPAddress.Parse("192.169.1.1")));
        }

        [Fact]
        public void An_ipv6_address_is_not_answered_with_an_ipv4_rule()
        {
            // GetIp4Address is the only caller and filters to IPv4 first, but the method is public and
            // says nothing about that. Answering "false" is honest; reading four bytes that are not
            // there would not be.
            Assert.False(Network.IsInLocalIpRange(IPAddress.Parse("fd1a::1")));
            Assert.False(Network.IsInLocalIpRange(IPAddress.IPv6Loopback));
        }

        [Fact]
        public void No_address_at_all_is_not_local()
            => Assert.False(Network.IsInLocalIpRange(null!));
    }
}
