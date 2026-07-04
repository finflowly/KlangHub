using KlangHub.Discover;

namespace KlangHub.Platform.Casting.Chromecast
{
    /// <summary>
    /// Single source of truth for a Chromecast endpoint's stable, non-empty id (USN → MAC → host:port).
    /// Used by discovery (to key descriptors) AND by the Device-hosted session, so both agree and the
    /// provider can join a descriptor back to its concrete device.
    /// </summary>
    internal static class ChromecastDeviceId
    {
        public static string From(DiscoveredDevice device)
        {
            if (device == null) return string.Empty;
            if (!string.IsNullOrEmpty(device.Usn)) return device.Usn;
            if (!string.IsNullOrEmpty(device.MACAddress)) return device.MACAddress;
            if (!string.IsNullOrEmpty(device.IPAddress)) return $"{device.IPAddress}:{device.Port}";
            return device.Id ?? string.Empty;
        }
    }
}
