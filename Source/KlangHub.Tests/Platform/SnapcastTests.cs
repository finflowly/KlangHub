using System;
using System.Collections.Generic;
using KlangHub.Core.Casting;
using KlangHub.Platform.Casting.Shared;
using KlangHub.Platform.Casting.Snapcast;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class SnapcastTests
    {
        private static MdnsService Service(string instance, string host, string address, int port) =>
            new(instance, host, address, port, new Dictionary<string, string>());

        [Fact]
        public void ToDescriptor_maps_a_snapserver_to_one_endpoint_keyed_by_address_port()
        {
            var d = SnapcastDiscovery.ToDescriptor(Service("Snapserver", "pi.local", "192.168.0.5", 1704));

            Assert.Equal("192.168.0.5:1704", d.Id);
            Assert.Equal("Snapserver", d.Name);
            Assert.Equal(ProviderId.Snapcast, d.Provider);
            Assert.False(d.IsGroup);
        }

        [Fact]
        public void ToDescriptor_falls_back_to_hostname_then_id_for_the_name()
        {
            Assert.Equal("pi.local", SnapcastDiscovery.ToDescriptor(Service("", "pi.local", "192.168.0.5", 1704)).Name);
            Assert.Equal("192.168.0.5:1704", SnapcastDiscovery.ToDescriptor(Service("", "", "192.168.0.5", 1704)).Name);
        }

        [Fact]
        public void ToDescriptor_returns_null_for_an_incomplete_announcement()
        {
            Assert.Null(SnapcastDiscovery.ToDescriptor(Service("s", "h", "", 1704)));
            Assert.Null(SnapcastDiscovery.ToDescriptor(Service("s", "h", "1.2.3.4", 0)));
        }

        [Fact]
        public void Provider_is_a_serverfed_discovery_only_shell()
        {
            var provider = new SnapcastProvider(new SnapcastDiscovery(new MdnsDiscovery()));

            Assert.Equal(ProviderId.Snapcast, provider.Id);
            Assert.Equal(DeliveryModel.ServerFed, provider.Capabilities.DeliveryModel);
            Assert.True(provider.Capabilities.ConsumesLocalCapture);
            Assert.Throws<NotSupportedException>(() =>
                provider.CreateSession(new CastDeviceDescriptor("id", "x", ProviderId.Snapcast, false)));
        }
    }
}
