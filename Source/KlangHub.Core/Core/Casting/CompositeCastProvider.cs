using System;
using System.Collections.Generic;
using System.Linq;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// 2.2b-H4a: an <see cref="ICastProvider"/> that fronts N real providers, so the app and the
    /// composition root still deal with a single provider. <see cref="CreateSession"/> routes by
    /// <see cref="CastDeviceDescriptor.Provider"/>; <see cref="Discovery"/> fans discovery out to every
    /// provider and merges their <see cref="IDeviceDiscovery.DeviceDiscovered"/> streams. Additive and
    /// behaviour-neutral with a single Chromecast provider registered; a second provider (AirPlay 2,
    /// Snapcast, ...) is enabled by passing it to the constructor.
    /// </summary>
    public sealed class CompositeCastProvider : ICastProvider
    {
        private readonly IReadOnlyDictionary<ProviderId, ICastProvider> providers;
        private readonly CompositeDeviceDiscovery discovery;

        public CompositeCastProvider(IEnumerable<ICastProvider> providersIn)
        {
            providers = providersIn.ToDictionary(p => p.Id);
            discovery = new CompositeDeviceDiscovery(providers.Values.Select(p => p.Discovery));
        }

        public ProviderId Id => ProviderId.Composite;

        /// <summary>Union of the fronted providers' capabilities.</summary>
        public CastProviderCapabilities Capabilities => new CastProviderCapabilities(
            ConsumesLocalCapture: providers.Values.Any(p => p.Capabilities.ConsumesLocalCapture),
            SupportsGrouping: providers.Values.Any(p => p.Capabilities.SupportsGrouping),
            SupportsVolumeControl: providers.Values.Any(p => p.Capabilities.SupportsVolumeControl),
            RequiresPairing: providers.Values.Any(p => p.Capabilities.RequiresPairing));
        // DeliveryModel is left at its default: it is per-provider and not meaningful aggregated on the composite.

        public IDeviceDiscovery Discovery => discovery;

        public IPlaybackSession CreateSession(CastDeviceDescriptor device)
        {
            if (device == null || !providers.TryGetValue(device.Provider, out var provider))
                throw new InvalidOperationException(
                    $"No cast provider registered for '{device?.Provider}' (device '{device?.Name}').");
            return provider.CreateSession(device);
        }

        public void Dispose()
        {
            // Per the ICastProvider contract each provider disposes its own Discovery.
            foreach (var provider in providers.Values)
                provider.Dispose();
        }
    }

    /// <summary>Fans Start/Stop/Dispose out to every provider's discovery and merges DeviceDiscovered.</summary>
    internal sealed class CompositeDeviceDiscovery : IDeviceDiscovery
    {
        private readonly IReadOnlyList<IDeviceDiscovery> inner;

        public event EventHandler<CastDeviceDescriptor>? DeviceDiscovered;

        public CompositeDeviceDiscovery(IEnumerable<IDeviceDiscovery> innerIn)
        {
            inner = innerIn.ToList();
            foreach (var d in inner)
                d.DeviceDiscovered += (s, e) => DeviceDiscovered?.Invoke(this, e);
        }

        public void Start() { foreach (var d in inner) d.Start(); }

        public void Stop() { foreach (var d in inner) d.Stop(); }

        public void Dispose() { foreach (var d in inner) d.Dispose(); }
    }
}
