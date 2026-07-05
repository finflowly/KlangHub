using System;
using System.Collections.Generic;
using KlangHub.Core.Casting;
using KlangHub.Core.Diagnostics;
using KlangHub.Platform.Casting.AirPlay;
using KlangHub.Platform.Casting.Shared;
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class AirPlayTests
    {
        private static MdnsService Service(string instance, string host, string address, params (string, string)[] txt)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in txt) dict[k] = v;
            return new MdnsService(instance, host, address, 7000, dict);
        }

        [Fact]
        public void ToDescriptor_uses_deviceid_as_id_and_the_instance_as_name()
        {
            var d = AirPlayDiscovery.ToDescriptor(
                Service("Living Room", "lr.local", "192.168.0.9", ("deviceid", "00:11:22:33:44:55"), ("model", "AudioAccessory5,1")));

            Assert.Equal("00:11:22:33:44:55", d!.Id);
            Assert.Equal("Living Room", d.Name);
            Assert.Equal(ProviderId.AirPlay, d.Provider);
            Assert.False(d.IsGroup);
        }

        [Fact]
        public void ToDescriptor_strips_the_mac_prefix_from_a_raop_instance_name()
        {
            var d = AirPlayDiscovery.ToDescriptor(
                Service("001122334455@Kitchen", "k.local", "192.168.0.10", ("pk", "abcdef")));

            Assert.Equal("abcdef", d!.Id);          // no deviceid -> pk
            Assert.Equal("Kitchen", d.Name);        // "<MAC>@Kitchen" -> "Kitchen"
        }

        [Theory]
        [InlineData("AirPlay2", "366.0", "1")]        // srcvers present -> AirPlay 2
        [InlineData("AirPlay2", null, "1,3")]         // encryption type 3 (FairPlay) -> AirPlay 2
        [InlineData("LegacyRaop", null, "1")]         // only AES -> legacy RAOP
        [InlineData("Unknown", null, null)]           // no signals
        public void Classify_distinguishes_legacy_raop_from_airplay2(string expected, string? srcvers, string? et)
        {
            var txt = new List<(string, string)>();
            if (srcvers != null) txt.Add(("srcvers", srcvers));
            if (et != null) txt.Add(("et", et));

            var kind = AirPlayDiscovery.Classify(Service("x", "h", "1.2.3.4", txt.ToArray()));

            Assert.Equal(expected, kind.ToString());
        }

        [Fact]
        public void Provider_is_a_pushrtp_pairing_discovery_only_shell()
        {
            var provider = new AirPlayProvider(new AirPlayDiscovery(new MdnsDiscovery(), Substitute.For<ILogger>()));

            Assert.Equal(ProviderId.AirPlay, provider.Id);
            Assert.Equal(DeliveryModel.PushRtp, provider.Capabilities.DeliveryModel);
            Assert.True(provider.Capabilities.RequiresPairing);
            Assert.Throws<NotSupportedException>(() =>
                provider.CreateSession(new CastDeviceDescriptor("id", "x", ProviderId.AirPlay, false)));
        }
    }
}
