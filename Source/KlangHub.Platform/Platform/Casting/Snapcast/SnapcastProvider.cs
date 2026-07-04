using System;

namespace KlangHub.Platform.Casting.Snapcast
{
    /// <summary>
    /// 2.2b-M3: discovery-only shell for Snapcast. Snapcast is a one-server/N-synced-clients system
    /// (DeliveryModel.ServerFed): KlangHub feeds one PCM stream into a snapserver and controls its clients
    /// via JSON-RPC. The control session (<see cref="CreateSession"/>) is implemented in M4; today it throws.
    /// </summary>
    public sealed class SnapcastProvider : ICastProvider
    {
        private readonly SnapcastDiscovery discovery;

        public SnapcastProvider(SnapcastDiscovery discoveryIn)
        {
            discovery = discoveryIn;
        }

        public ProviderId Id => ProviderId.Snapcast;

        public CastProviderCapabilities Capabilities { get; } = new(
            ConsumesLocalCapture: true,
            SupportsGrouping: true,
            SupportsVolumeControl: true,
            DeliveryModel: DeliveryModel.ServerFed,
            RequiresPairing: false);

        public IDeviceDiscovery Discovery => discovery;

        public IPlaybackSession CreateSession(CastDeviceDescriptor device) =>
            throw new NotSupportedException("Snapcast control sessions are not implemented yet (M4).");

        public void Dispose() => discovery.Dispose();
    }
}
