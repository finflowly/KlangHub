using System.Net;

namespace KlangHub.Core.Models
{
    /// <summary>
    /// Represents a PC's network adapter.
    /// </summary>
    public class NetworkAdapter
    {
        public IPAddress IPAddress { get; set; }
        public bool IsEthernet { get; set; }
    }
}
