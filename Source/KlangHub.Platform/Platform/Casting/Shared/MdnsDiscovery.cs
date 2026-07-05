using System;
using System.Collections.Generic;
using Tmds.MDns;

namespace KlangHub.Platform.Casting.Shared
{
    /// <summary>A neutral view of one discovered Bonjour/mDNS service announcement.</summary>
    public sealed record MdnsService(
        string Instance,
        string HostName,
        string Address,
        int Port,
        IReadOnlyDictionary<string, string> Txt);

    /// <summary>
    /// 2.2b-M3: a generic Tmds.MDns browser, reusable across providers for arbitrary service types
    /// (`_airplay._tcp`, `_raop._tcp`, `_snapcast._tcp`, ...). It raises a neutral <see cref="MdnsService"/>
    /// per announcement; each provider's discovery interprets the TXT keys. Chromecast keeps its own bespoke
    /// discovery (DiscoverDevices) - this helper is only for the new providers.
    /// </summary>
    public sealed class MdnsDiscovery : IDisposable
    {
        private readonly List<ServiceBrowser> browsers = new();
        private bool started;

        /// <summary>Raised for each discovered service (on Tmds.MDns's callback thread).</summary>
        public event Action<MdnsService>? ServiceFound;

        /// <summary>Begin browsing the given service types. Idempotent: only the first call browses
        /// (mDNS discovery is continuous), so repeated ScanForDevices calls do not stack browsers.</summary>
        public void Start(params string[] serviceTypes)
        {
            if (started || serviceTypes == null)
                return;
            started = true;

            foreach (var type in serviceTypes)
            {
                try
                {
                    var browser = new ServiceBrowser();
                    browser.ServiceAdded += OnServiceAdded;
                    browser.StartBrowse(type);
                    browsers.Add(browser);
                }
                catch (Exception)
                {
                    // a browse failure on one type must not kill discovery of the others
                }
            }
        }

        // mDNS is continuous and torn down with the process, matching the Chromecast discovery.
        public void Stop() { }

        public void Dispose() { }

        private void OnServiceAdded(object? sender, ServiceAnnouncementEventArgs e)
        {
            var a = e?.Announcement;
            if (a == null || a.Addresses == null || a.Addresses.Count == 0)
                return;

            ServiceFound?.Invoke(new MdnsService(
                Instance: a.Instance,
                HostName: a.Hostname,
                Address: a.Addresses[0].ToString(),
                Port: a.Port,
                Txt: ParseTxt(a.Txt)));
        }

        /// <summary>Parse Tmds.MDns's "key=value" TXT records into a case-insensitive dictionary
        /// (first value wins; a bare key maps to empty string).</summary>
        internal static IReadOnlyDictionary<string, string> ParseTxt(IList<string>? txt)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (txt == null)
                return dict;

            foreach (var entry in txt)
            {
                if (string.IsNullOrEmpty(entry))
                    continue;

                var i = entry.IndexOf('=');
                var key = i < 0 ? entry : entry.Substring(0, i);
                var value = i < 0 ? string.Empty : entry.Substring(i + 1);
                if (!dict.ContainsKey(key))
                    dict[key] = value;
            }
            return dict;
        }
    }
}
