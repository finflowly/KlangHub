using KlangHub.Discover;

namespace KlangHub.Platform.Casting.Chromecast
{
    /// <summary>
    /// Single source of truth for a Chromecast endpoint's stable, non-empty id (USN → real MAC → host:port).
    /// Used by discovery (to key descriptors) AND by the Device-hosted session, so both agree and the
    /// provider can join a descriptor back to its concrete device.
    /// </summary>
    internal static class ChromecastDeviceId
    {
        // Google TV / Android TV / several speakers report this placeholder MAC in eureka_info; it is NOT a
        // unique identity, so it must never key an id (else those devices share one id and their tiles all
        // resolve to a single session - 3 "Enchant Speaker" tiles that light up together).
        private const string PlaceholderMac = "00:00:00:00:00:00";

        public static string From(DiscoveredDevice device)
        {
            if (device == null) return string.Empty;
            if (!string.IsNullOrEmpty(device.Usn)) return device.Usn;
            if (!string.IsNullOrEmpty(device.MACAddress) && device.MACAddress != PlaceholderMac)
                return device.MACAddress;
            if (!string.IsNullOrEmpty(device.IPAddress)) return $"{device.IPAddress}:{device.Port}";
            return device.Id ?? string.Empty;
        }
    }
}
