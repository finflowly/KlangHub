using System;

namespace KlangHub.Platform.Casting.AirPlay
{
    /// <summary>
    /// 2.2b-M3: discovery-only shell for AirPlay. Streaming needs transient HomeKit pairing (SRP-6a +
    /// X25519/Ed25519 + ChaCha20) + an ALAC/RTP sender (DeliveryModel.PushRtp, RequiresPairing) - a large,
    /// dedicated later spike. This sprint only surfaces + classifies receivers; <see cref="CreateSession"/>
    /// throws.
    /// </summary>
    public sealed class AirPlayProvider : ICastProvider
    {
        private readonly AirPlayDiscovery discovery;

        public AirPlayProvider(AirPlayDiscovery discoveryIn)
        {
            discovery = discoveryIn;
        }

        public ProviderId Id => ProviderId.AirPlay;

        public CastProviderCapabilities Capabilities { get; } = new(
            ConsumesLocalCapture: true,
            SupportsGrouping: false,
            SupportsVolumeControl: true,
            DeliveryModel: DeliveryModel.PushRtp,
            RequiresPairing: true);

        public IDeviceDiscovery Discovery => discovery;

        public IPlaybackSession CreateSession(CastDeviceDescriptor device) =>
            throw new NotSupportedException("AirPlay sessions require pairing + an encrypted RTP sender; deferred to a later spike.");

        public void Dispose() => discovery.Dispose();
    }
}
