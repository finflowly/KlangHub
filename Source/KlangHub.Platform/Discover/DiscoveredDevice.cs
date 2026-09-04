using KlangHub.Application;
using KlangHub.Communication;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace KlangHub.Discover
{
    public class DiscoveredDevice
    {
        private const string GroupIdentifier = "\"md=Google Cast Group\"";

        public string Name { get; set; } = null!;
        public string IPAddress { get; set; } = null!;

        /// <summary>All addresses (IPv4 + IPv6) seen for this device across mDNS announcements + eureka, so a
        /// device discovered over IPv6 can still be matched to its IPv4 stream-connect-back socket. Runtime-only
        /// (not persisted). <see cref="IPAddress"/> stays the "primary" used for eureka/:8009.</summary>
        [XmlIgnore]
        public List<string> Addresses { get; set; } = new List<string>();
        public int Port { get; set; }
        public string Protocol { get; set; } = null!;
        public string Usn { get; set; } = null!;
        public string Headers { get; set; } = null!;
        public bool AddedByDeviceInfo { get; set; }
        [XmlIgnore]
        public DeviceEureka Eureka { get; set; } = null!;
        [XmlIgnore]
        public Group Group { get; set; } = null!;
        public DeviceState DeviceState { get; set; }
        public bool IsGroup {
            get
            {
                if (Headers != null && Headers.IndexOf(GroupIdentifier) >= 0)
                    return true;

                if (Port != 8009)
                    return true;

                return false;
            }
            set
            {
                if (value)
                    Headers = GroupIdentifier;
            }
        }

        /// <summary>
        /// The hardware model this endpoint announces in its mDNS TXT record ("md=Google Nest Audio").
        /// <see cref="Headers"/> holds those tokens as one serialized string, so read up to the next quote or
        /// separator. Null for groups (their "model" is the literal "Google Cast Group") and when nothing is
        /// announced - the UI then falls back to a generic subtitle rather than showing a fragment.
        /// </summary>
        [XmlIgnore]
        public string? ModelName
        {
            get
            {
                // eureka is the better source when it has been fetched (it names the real product, e.g.
                // "Harman Kardon Enchant"); the mDNS TXT record is the fallback for groups and for devices
                // whose eureka_info call has not landed yet.
                var mf = Eureka?.DeviceInfo?.Manufacturer?.Trim();
                var md = Eureka?.DeviceInfo?.Model_name?.Trim();
                if (!string.IsNullOrEmpty(md))
                    return !string.IsNullOrEmpty(mf)
                           && md!.IndexOf(mf!, System.StringComparison.OrdinalIgnoreCase) < 0
                        ? $"{mf} {md}"
                        : md;

                return ParseModel(Headers);
            }
        }

        internal static string? ParseModel(string? headers)
        {
            if (string.IsNullOrEmpty(headers))
                return null;

            int i = headers!.IndexOf("md=", System.StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return null;

            i += 3;
            int end = headers.IndexOfAny(new[] { '"', ';', '\n', '\r' }, i);
            var model = (end < 0 ? headers[i..] : headers[i..end]).Trim();
            if (model.Length == 0 || model.Equals("Google Cast Group", System.StringComparison.OrdinalIgnoreCase))
                return null;

            return model;
        }

        public string MACAddress { get; set; } = null!;
        public string Id { get; set; } = null!;
    }
}
