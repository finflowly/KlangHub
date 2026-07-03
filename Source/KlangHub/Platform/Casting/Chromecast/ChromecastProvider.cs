using System;

namespace KlangHub.Platform.Casting.Chromecast
{
    /// <summary>
    /// The Chromecast <see cref="ICastProvider"/>: owns mDNS discovery and (later) creates playback
    /// sessions. Chromecast consumes KlangHub's local capture stream, so <c>ConsumesLocalCapture</c>
    /// is true — this is the bridge to the audio seam.
    ///
    /// NOTE: <see cref="CreateSession"/> is intentionally not implemented yet. Chromecast sessions are
    /// hosted by the existing <c>Device</c> (which owns state + connection + streaming); wiring
    /// <c>Device</c> to <see cref="IPlaybackSession"/> is the sensitive step 2.2b-2.
    /// </summary>
    public sealed class ChromecastProvider : ICastProvider
    {
        private readonly ChromecastDeviceDiscovery discovery;

        public ChromecastProvider(ChromecastDeviceDiscovery discoveryIn)
        {
            discovery = discoveryIn;
        }

        public ProviderId Id => ProviderId.Chromecast;

        public CastProviderCapabilities Capabilities { get; } = new(
            ConsumesLocalCapture: true,
            SupportsGrouping: true,
            SupportsVolumeControl: true);

        public IDeviceDiscovery Discovery => discovery;

        public IPlaybackSession CreateSession(CastDeviceDescriptor device)
        {
            throw new NotSupportedException(
                "Chromecast sessions are hosted by the Device that owns the connection and streaming. " +
                "Wiring Device to IPlaybackSession is step 2.2b-2.");
        }

        public void Dispose()
        {
            discovery.Dispose();
        }
    }
}
