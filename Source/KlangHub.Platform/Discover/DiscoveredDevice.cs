using KlangHub.Application;
using KlangHub.Communication;
using System.Xml.Serialization;

namespace KlangHub.Discover
{
    public class DiscoveredDevice
    {
        private const string GroupIdentifier = "\"md=Google Cast Group\"";

        public string Name { get; set; } = null!;
        public string IPAddress { get; set; } = null!;
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

        public string MACAddress { get; set; } = null!;
        public string Id { get; set; } = null!;
    }
}
