using System;
using KlangHub.Core.Casting;
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class CompositeCastProviderTests
    {
        private static ICastProvider FakeProvider(ProviderId id, CastProviderCapabilities caps = null,
            IDeviceDiscovery discovery = null)
        {
            var provider = Substitute.For<ICastProvider>();
            provider.Id.Returns(id);
            provider.Discovery.Returns(discovery ?? Substitute.For<IDeviceDiscovery>());
            provider.Capabilities.Returns(caps ?? new CastProviderCapabilities(true, true, true));
            return provider;
        }

        [Fact]
        public void CreateSession_routes_to_the_provider_matching_the_descriptor()
        {
            var session = Substitute.For<IPlaybackSession>();
            var chromecast = FakeProvider(ProviderId.Chromecast);
            var descriptor = new CastDeviceDescriptor("id-1", "Speaker", ProviderId.Chromecast, false);
            chromecast.CreateSession(descriptor).Returns(session);

            var composite = new CompositeCastProvider(new[] { chromecast });

            Assert.Same(session, composite.CreateSession(descriptor));
            chromecast.Received(1).CreateSession(descriptor);
        }

        [Fact]
        public void CreateSession_throws_when_no_provider_is_registered_for_the_descriptor()
        {
            var composite = new CompositeCastProvider(new[] { FakeProvider(ProviderId.Chromecast) });
            var descriptor = new CastDeviceDescriptor("id", "AirPlay dev", ProviderId.AirPlay, false);

            Assert.Throws<InvalidOperationException>(() => composite.CreateSession(descriptor));
        }

        [Fact]
        public void Discovery_Start_fans_out_to_every_provider_discovery()
        {
            var d1 = Substitute.For<IDeviceDiscovery>();
            var d2 = Substitute.For<IDeviceDiscovery>();
            var composite = new CompositeCastProvider(new[]
            {
                FakeProvider(ProviderId.Chromecast, discovery: d1),
                FakeProvider(ProviderId.AirPlay, discovery: d2),
            });

            composite.Discovery.Start();

            d1.Received(1).Start();
            d2.Received(1).Start();
        }

        [Fact]
        public void Discovery_merges_a_providers_DeviceDiscovered_event()
        {
            var d1 = Substitute.For<IDeviceDiscovery>();
            var composite = new CompositeCastProvider(new[] { FakeProvider(ProviderId.Chromecast, discovery: d1) });

            CastDeviceDescriptor received = null;
            composite.Discovery.DeviceDiscovered += (s, d) => received = d;

            var descriptor = new CastDeviceDescriptor("id", "Speaker", ProviderId.Chromecast, false);
            d1.DeviceDiscovered += Raise.Event<EventHandler<CastDeviceDescriptor>>(d1, descriptor);

            Assert.Same(descriptor, received);
        }

        [Fact]
        public void Capabilities_are_the_union_of_the_fronted_providers()
        {
            var chromecast = FakeProvider(ProviderId.Chromecast,
                new CastProviderCapabilities(ConsumesLocalCapture: true, SupportsGrouping: true, SupportsVolumeControl: false));
            var airplay = FakeProvider(ProviderId.AirPlay,
                new CastProviderCapabilities(ConsumesLocalCapture: false, SupportsGrouping: false, SupportsVolumeControl: true));

            var composite = new CompositeCastProvider(new[] { chromecast, airplay });
            var caps = composite.Capabilities;

            Assert.True(caps.ConsumesLocalCapture);
            Assert.True(caps.SupportsGrouping);
            Assert.True(caps.SupportsVolumeControl);
            Assert.Equal(ProviderId.Composite, composite.Id);
        }

        [Fact]
        public void Dispose_disposes_every_fronted_provider()
        {
            var chromecast = FakeProvider(ProviderId.Chromecast);
            var airplay = FakeProvider(ProviderId.AirPlay);
            var composite = new CompositeCastProvider(new[] { chromecast, airplay });

            composite.Dispose();

            chromecast.Received(1).Dispose();
            airplay.Received(1).Dispose();
        }
    }
}
