using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// A casting protocol as a first-class, swappable peer (Chromecast today; Spotify Connect,
    /// Amazon Alexa Cast, ... later). Owns its discovery and creates playback sessions for its devices.
    /// Disposing the provider disposes its <see cref="Discovery"/>; sessions from
    /// <see cref="CreateSession"/> are owned (and disposed) by the caller.
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
