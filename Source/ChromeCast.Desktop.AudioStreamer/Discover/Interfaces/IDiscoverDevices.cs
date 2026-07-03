using System;

namespace KlangHub.Discover.Interfaces
{
    public interface IDiscoverDevices
    {
        void Discover(Action<DiscoveredDevice> onDiscovered);
        void Dispose();
    }
}