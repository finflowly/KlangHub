using System;
using System.Collections.Generic;
using KlangHub.Discover;
using KlangHub.Discover.Interfaces;

namespace KlangHub.Platform.Casting.Chromecast
{
    /// <summary>
    /// Wraps the existing mDNS <see cref="IDiscoverDevices"/> as a neutral <see cref="IDeviceDiscovery"/>.
    /// Emits <see cref="CastDeviceDescriptor"/>s and keeps an Id -&gt; real <see cref="DiscoveredDevice"/> map
    /// so the provider can re-find the concrete device (which holds USN/MAC/Eureka) when opening a session.
    /// The existing DiscoverDevices logic is left unchanged.
    /// </summary>
    public sealed class ChromecastDeviceDiscovery : IDeviceDiscovery
    {
        private readonly IDiscoverDevices inner;
        private readonly Dictionary<string, DiscoveredDevice> byId = new();
        private readonly object sync = new();
        private bool started;

        public event EventHandler<CastDeviceDescriptor> DeviceDiscovered;

        public ChromecastDeviceDiscovery(IDiscoverDevices innerIn)
        {
            inner = innerIn;
        }

        public void Start()
        {
            if (started)
                return;

            started = true;
            inner.Discover(OnDiscovered);
        }

        // mDNS discovery is continuous and has no explicit stop; it is torn down on Dispose.
        public void Stop() { }

        /// <summary>
        /// Chromecast-internal: re-find the concrete device a descriptor's Id came from.
        /// (Not on IDeviceDiscovery — only the Chromecast provider needs it.)
        /// </summary>
        public bool TryGetDevice(string id, out DiscoveredDevice device)
        {
            lock (sync)
            {
                return byId.TryGetValue(id, out device);
            }
        }

        public void Dispose()
        {
            inner.Dispose();
        }

        private void OnDiscovered(DiscoveredDevice device)
        {
            if (device == null)
                return;

            var id = StableId(device);
            if (string.IsNullOrEmpty(id))
                return;

            lock (sync)
            {
                byId[id] = device;
            }

            DeviceDiscovered?.Invoke(this, new CastDeviceDescriptor(id, device.Name, ProviderId.Chromecast, device.IsGroup));
        }

        /// <summary>A stable, non-empty id: prefer the mDNS USN, then MAC, then host:port.</summary>
        private static string StableId(DiscoveredDevice device)
        {
            if (!string.IsNullOrEmpty(device.Usn)) return device.Usn;
            if (!string.IsNullOrEmpty(device.MACAddress)) return device.MACAddress;
            if (!string.IsNullOrEmpty(device.IPAddress)) return $"{device.IPAddress}:{device.Port}";
            return device.Id;
        }
    }
}
