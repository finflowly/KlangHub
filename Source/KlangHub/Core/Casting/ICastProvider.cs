using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// A casting protocol as a first-class, swappable peer (Chromecast today; Spotify Connect,
    /// Amazon Alexa Cast, ... later). Owns its discovery and creates playback sessions for its devices.
    /// Disposing the provider disposes its <see cref="Discovery"/>. Session ownership is provider-defined:
    /// a provider may return an owned, disposable session, or a NON-OWNING view over a shared endpoint.
    /// The Chromecast provider returns the latter - its session's <c>Dispose()</c> is a no-op and the
    /// device's lifetime stays with the provider/registry.
    /// </summary>
    public interface ICastProvider : IDisposable
    {
        /// <summary>Stable discriminator for this provider.</summary>
        ProviderId Id { get; }

        /// <summary>What this provider can do and whether it consumes the local capture stream.</summary>
        CastProviderCapabilities Capabilities { get; }

        /// <summary>Discovery for this provider's endpoints.</summary>
        IDeviceDiscovery Discovery { get; }

        /// <summary>Open a control session for one of this provider's endpoints.</summary>
        IPlaybackSession CreateSession(CastDeviceDescriptor device);
    }
}
