using System;
using KlangHub.Platform.Casting.Shared;

namespace KlangHub.Platform.Casting.Snapcast
{
    /// <summary>
    /// 2.2b-M3: discovers snapservers via mDNS (`_snapcast._tcp`) and reports each as one neutral
    /// <see cref="CastDeviceDescriptor"/> (the server endpoint; its clients/groups are controlled via the
    /// JSON-RPC API later, in M4). A manual host:port fallback also comes in M4.
    /// </summary>
    public sealed class SnapcastDiscovery : IDeviceDiscovery
    {
        private const string ServiceType = "_snapcast._tcp";
        private readonly MdnsDiscovery mdns;

        public event EventHandler<CastDeviceDescriptor> DeviceDiscovered;

        public SnapcastDiscovery(MdnsDiscovery mdnsIn)
        {
            mdns = mdnsIn;
            mdns.ServiceFound += OnServiceFound;
        }

        public void Start() => mdns.Start(ServiceType);

        public void Stop() => mdns.Stop();

        public void Dispose() => mdns.Dispose();

        private void OnServiceFound(MdnsService service)
        {
            var descriptor = ToDescriptor(service);
            if (descriptor != null)
                DeviceDiscovered?.Invoke(this, descriptor);
        }

        /// <summary>Map a discovered snapserver announcement to one endpoint descriptor, keyed by address:port.</summary>
        internal static CastDeviceDescriptor ToDescriptor(MdnsService service)
        {
            if (service == null || string.IsNullOrEmpty(service.Address) || service.Port <= 0)
                return null;

            var id = $"{service.Address}:{service.Port}";
            var name = !string.IsNullOrEmpty(service.Instance) ? service.Instance
                     : !string.IsNullOrEmpty(service.HostName) ? service.HostName
                     : id;
            return new CastDeviceDescriptor(id, name, ProviderId.Snapcast, false);
        }
    }
}
