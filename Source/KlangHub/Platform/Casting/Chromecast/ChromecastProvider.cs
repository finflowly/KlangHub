using System;

namespace KlangHub.Platform.Casting.Chromecast
{
    /// <summary>
    /// The Chromecast <see cref="ICastProvider"/>: owns mDNS discovery and creates playback sessions.
    /// Chromecast consumes KlangHub's local capture stream, so <c>ConsumesLocalCapture</c> is true —
    /// this is the bridge to the audio seam.
    ///
    /// Chromecast sessions are hosted by the existing <c>Device</c> (which owns state + connection +
    /// streaming and implements <see cref="IPlaybackSession"/>). <see cref="CreateSession"/> does not
    /// build a session; it joins a descriptor back to its concrete <c>Device</c> via a resolver injected
    /// by the composition root, keyed by the stable <see cref="CastDeviceDescriptor.Id"/>.
    /// </summary>
    public sealed class ChromecastProvider : ICastProvider
    {
        private readonly ChromecastDeviceDiscovery discovery;

        // Resolves a descriptor back to its Device-hosted IPlaybackSession. Injected by the composition
        // root (where the Devices registry lives) so this provider stays decoupled from the Application
        // layer. Returns null when no live device matches the descriptor (e.g. it went offline).
        private readonly Func<CastDeviceDescriptor, IPlaybackSession> resolveSession;

        public ChromecastProvider(
            ChromecastDeviceDiscovery discoveryIn,
            Func<CastDeviceDescriptor, IPlaybackSession> resolveSessionIn)
        {
            discovery = discoveryIn;
            resolveSession = resolveSessionIn;
        }

        public ProviderId Id => ProviderId.Chromecast;

        public CastProviderCapabilities Capabilities { get; } = new(
            ConsumesLocalCapture: true,
            SupportsGrouping: true,
            SupportsVolumeControl: true);

        public IDeviceDiscovery Discovery => discovery;

        public IPlaybackSession CreateSession(CastDeviceDescriptor device)
        {
            var session = resolveSession(device);
            if (session == null)
                throw new InvalidOperationException(
                    $"No active Chromecast device for '{device.Name}' (id '{device.Id}'); it may have gone offline.");
            return session;
        }

        public void Dispose()
        {
            discovery.Dispose();
        }
    }
}
