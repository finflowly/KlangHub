using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Discovers the playback endpoints of one provider (mDNS for Chromecast, an account/cloud
    /// listing for Spotify, ...). Provider-neutral: reports <see cref="CastDeviceDescriptor"/>s.
    /// </summary>
    public interface IDeviceDiscovery : IDisposable
    {
        /// <summary>Raised for each endpoint found (may fire repeatedly as devices appear).</summary>
        event EventHandler<CastDeviceDescriptor> DeviceDiscovered;

        void Start();
        void Stop();
    }
}
