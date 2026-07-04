using System.Collections.Generic;
using KlangHub.Application;                  // IDevices
using KlangHub.Application.Orchestration;   // SettingsService
using KlangHub.Communication;               // DeviceState
using KlangHub.Core.Diagnostics;            // ILogger
using KlangHub.Discover;                    // DiscoveredDevice
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Orchestration
{
    public class SettingsServiceTests
    {
        private static SettingsService NewService() => new SettingsService(Substitute.For<ILogger>());

        [Fact]
        public void WasPlaying_true_when_the_saved_device_matches_and_was_playing()
        {
            var service = NewService();
            service.Settings.ChromecastDiscoveredDevices = new List<DiscoveredDevice>
            {
                new DiscoveredDevice { Port = 8009, Name = "Speaker", DeviceState = DeviceState.Playing },
            };

            Assert.True(service.WasPlaying(new DiscoveredDevice { Port = 8009, Name = "Speaker" }));
        }

        [Fact]
        public void WasPlaying_false_for_a_different_name_or_a_non_playing_state()
        {
            var service = NewService();
            service.Settings.ChromecastDiscoveredDevices = new List<DiscoveredDevice>
            {
                new DiscoveredDevice { Port = 8009, Name = "Speaker", DeviceState = DeviceState.Idle },
                new DiscoveredDevice { Port = 8009, Name = "Other", DeviceState = DeviceState.Playing },
            };

            Assert.False(service.WasPlaying(new DiscoveredDevice { Port = 8009, Name = "Speaker" })); // idle
            Assert.False(service.WasPlaying(new DiscoveredDevice { Port = 8009, Name = "Missing" }));
        }

        [Fact]
        public void MergeDiscoveredHosts_adds_new_hosts_and_drops_entries_without_id_or_mac()
        {
            var service = NewService();
            service.Settings.ChromecastDiscoveredDevices = new List<DiscoveredDevice>
            {
                new DiscoveredDevice { IsGroup = false, MACAddress = "" }, // dropped by RemoveOldEntries
            };
            var devices = Substitute.For<IDevices>();
            devices.GetHosts().Returns(new List<DiscoveredDevice>
            {
                new DiscoveredDevice { IsGroup = false, MACAddress = "AA:BB", Name = "New", DeviceState = DeviceState.Connected },
            });

            service.MergeDiscoveredHosts(devices);

            var saved = service.Settings.ChromecastDiscoveredDevices;
            Assert.Single(saved);
            Assert.Equal("AA:BB", saved[0].MACAddress);
        }
    }
}
