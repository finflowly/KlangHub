using System;
using KlangHub.Discover.Interfaces;
using Tmds.MDns;
using System.Linq;
using System.Timers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace KlangHub.Discover
{
    public class DiscoverDevices : IDiscoverDevices
    {
        public const int Interval = 2000;
        public const int MaxNumberOfTries = 15;
        private const string serviceType = "_googlecast._tcp";
        private const string serviceTypeEmbedded = "_googlezone._tcp";
        // Co-located services browsed ONLY to feed Ipv4Recovery (the cross-service IPv4 bridge) - never to make tiles.
        private const string serviceTypeAirplay = "_airplay._tcp";
        private const string serviceTypeRaop = "_raop._tcp";
        private Action<DiscoveredDevice> onDiscovered = null!;
        private List<DiscoveredDevice>? discoveredDevices;
        private Timer? timer;
        private List<MsdnIps>? msdnIps;

        /// <summary>
        /// The browsers, held for the life of this object.
        ///
        /// They used to be local variables: StartBrowse was called and the method returned, leaving nothing
        /// referencing them. That worked only because the mDNS library happened to root them internally -
        /// and when Tmds.MDns 0.9.1 changed exactly that ("robustness improvements to the root timer to
        /// prevent garbage collection"), the browsers were collected mid-run and discovery went silent:
        /// "Suche nach Geräten..." forever, no tiles, nothing in the log. MdnsDiscovery on the other side
        /// of the app always kept its own list, which is why only this path broke.
        ///
        /// Relying on a library to keep our objects alive was wrong regardless of which version does it.
        /// </summary>
        private readonly List<ServiceBrowser> browsers = new();
        private bool browsing;
        private readonly ILogger? logger;

        /// <summary>How many browsers are live. Must not grow with the number of scans; see the tests.</summary>
        internal int BrowserCount => browsers.Count;

        /// <summary>Seam so tests can exercise the browser bookkeeping without opening a socket.</summary>
        internal virtual void StartBrowser(ServiceBrowser browser, string serviceType)
            => browser.StartBrowse(serviceType);

        // IPv4 recovery for a device that announces its _googlecast IPv6-only in a scan (see Ipv4Recovery).
        private readonly Ipv4Recovery ipv4Recovery = new Ipv4Recovery();

        public DiscoverDevices(ILogger? loggerIn = null)
        {
            logger = loggerIn;
        }

        /// <summary>
        /// Start discovering devices.
        /// </summary>
        /// <param name="onDiscoveredIn">callback for when a device is discovered</param>
        public void Discover(Action<DiscoveredDevice> onDiscoveredIn)
        {
            if (onDiscoveredIn == null)
                return;

            onDiscovered = onDiscoveredIn;

            // ScanForDevices calls this per scan and on every IP change. Only the first call builds the
            // queue, the drain timer and the browsers; the later ones would otherwise throw away a queue
            // that announcements are still being written into and leave the old timer running beside the
            // new one, each delivering the same devices.
            if (timer == null)
            {
                discoveredDevices = new List<DiscoveredDevice>();
                timer = new Timer
                {
                    Interval = 500,
                    Enabled = true
                };
                timer.Elapsed += new ElapsedEventHandler(OnAddDevice);
                timer.Start();
                msdnIps = new List<MsdnIps>();
            }

            // MDNS search
            MdnsSearch();
        }

        /// <summary>
        /// Do a scan on the network.
        /// </summary>
        public void MdnsSearch()
        {
            // Idempotent, like MdnsDiscovery on the other side of the app. mDNS browsing is CONTINUOUS: once
            // a browser runs it keeps announcing and receiving, so a second scan needs no second set. While
            // the browsers were local variables the extra ones were simply collected and nobody noticed; now
            // that they are held for the life of this object (so the GC cannot take them mid-search), every
            // scan would add four more live browsers with a socket on every interface - for the whole session.
            if (browsing)
                return;
            browsing = true;

            foreach (var type in new[] { serviceType, serviceTypeEmbedded })
            {
                var browser = new ServiceBrowser();
                browser.ServiceAdded += OnServiceAdded;
                browser.ServiceRemoved += OnServiceRemoved;
                browser.ServiceChanged += OnServiceChanged;
                StartBrowser(browser, type);
                browsers.Add(browser);
            }

            // Cross-service IPv4 bridge. A Chromecast-built-in speaker that flaps its _googlecast IPv6-only (the
            // Harman Kardon Enchant) usually still advertises an IPv4 on its CO-LOCATED _airplay/_raop service
            // under the SAME IPv6 host. Browse those services purely to record the IPv4<->IPv6-host correlation
            // in Ipv4Recovery, so an IPv6-only _googlecast announcement can recover a usable IPv4. These browsers
            // create NO tiles (only _googlecast/_googlezone announcements enqueue devices - see the protocol
            // guard in ProcessAnnouncement); they are correlation sources only. Strictly additive + recover-or-
            // skip: a device that announces IPv4 on _googlecast never consults recovery, so this cannot regress
            // the hardware-confirmed IPv4 discovery. (Efficacy is HW-confirmable: it helps iff the co-located
            // service carries an IPv4 with the shared host; if it too is IPv6-only, the bridge is simply inert.)
            foreach (var coLocated in new[] { serviceTypeAirplay, serviceTypeRaop })
            {
                var correlationBrowser = new ServiceBrowser();
                correlationBrowser.ServiceAdded += OnCorrelationService;
                correlationBrowser.ServiceChanged += OnCorrelationService;
                StartBrowser(correlationBrowser, coLocated);
                browsers.Add(correlationBrowser);
            }
        }

        /// <summary>Record an IPv4↔IPv6-host correlation from a co-located non-Cast service (AirPlay/RAOP)
        /// WITHOUT enqueueing a device. Feeds the cross-service IPv4 bridge in <see cref="Ipv4Recovery"/>.</summary>
        private void OnCorrelationService(object? sender, ServiceAnnouncementEventArgs e)
        {
            if (e?.Announcement?.Addresses == null || e.Announcement.Addresses.Count == 0)
                return;

            var ipv4 = e.Announcement.Addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            if (ipv4 == null)
                return; // co-located service is IPv6-only too - nothing to bridge

            // id=null: the AirPlay/RAOP id space differs from the Cast id=, so only the shared-IPv6-host mapping
            // is meaningful here (and it can never collide with a real Cast id).
            ipv4Recovery.Record(null, ipv4, e.Announcement.Addresses);

            var hosts = e.Announcement.Addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(a => a.ToString());
            logger?.Log($"mDNS-bridge [{e.Announcement.Type}] learned IPv4 {ipv4} for hosts=[{string.Join(", ", hosts)}]");
        }

        /// <summary>
        /// Callback for when a device announcement changes. A device often sends its A (IPv4) record AFTER an
        /// initial AAAA-only announcement; that late IPv4 arrives here as a CHANGE, not an add. Process it the
        /// same way so the IPv4 is captured - previously this was a no-op, which left an IPv6-only-flapping
        /// device (the Enchant) without a tile whenever its A record arrived late.
        /// </summary>
        private void OnServiceChanged(object? sender, ServiceAnnouncementEventArgs e)
        {
            ProcessAnnouncement(e, "chg");
        }

        /// <summary>
        /// Callback for when a device is removed.
        /// </summary>
        private void OnServiceRemoved(object? sender, ServiceAnnouncementEventArgs e)
        {
            //TODO
        }

        /// <summary>
        /// Callback for when a device is added.
        /// </summary>
        private void OnServiceAdded(object? sender, ServiceAnnouncementEventArgs e)
        {
            ProcessAnnouncement(e, "add");
        }

        /// <summary>Process one mDNS announcement (add or change): record IPv4 correlations, pick/recover a
        /// usable IPv4, and enqueue the device. Runs for both ServiceAdded and ServiceChanged so a late A record
        /// is not missed.</summary>
        private void ProcessAnnouncement(ServiceAnnouncementEventArgs e, string source)
        {
            if (e == null || e.Announcement == null || e.Announcement.Addresses == null || e.Announcement.Addresses.Count == 0
                || e.Announcement.Txt == null || discoveredDevices == null)
                return;

            // KlangHub reaches devices over IPv4 (the http://<ip>:8008 eureka_info URL and the :8009 cast
            // connection). A device often sends separate mDNS announcements per address family. We prefer IPv4,
            // but if an announcement is IPv6-only we no longer drop it: first try to RECOVER a usable IPv4 (the
            // device self-advertised IPv4 earlier, keyed by id=, or the dual-stack group it hosts donates it via
            // a shared IPv6 host); otherwise keep the IPv6 literal so a truly-IPv6-only device is still reachable.
            var addresses = e.Announcement.Addresses.Select(a => a.ToString()).ToList();
            var ipv4 = e.Announcement.Addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            var ipv6 = e.Announcement.Addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)?.ToString();
            var id = e.Announcement.Txt.Where(x => x.ToString().StartsWith("id=")).FirstOrDefault()?.Replace("id=", "");

            ipv4Recovery.Record(id, ipv4, e.Announcement.Addresses);

            // Reach devices over IPv4 only: prefer the announced IPv4, else recover it (by id=, then via the
            // dual-stack group the device hosts). We do NOT enqueue a raw IPv6 literal - keying a tile on it
            // would create an ordering-dependent duplicate that never dedups against the device's IPv4 tile
            // (and a truly-IPv6-only Cast target can't be streamed to anyway; the audio return path is IPv4).
            // The correlation cache persists and mDNS re-announces (~2s), so an IPv6-only-first device is
            // recovered on a later scan; until then it is skipped exactly as before (self-healing).
            var recoverySource = string.Empty;
            var recovered = ipv4 == null ? ipv4Recovery.Recover(id, ipv6, out recoverySource) : null;
            var primary = ipv4 ?? recovered;

            var fnDbg = e.Announcement.Txt.FirstOrDefault(x => x.ToString().StartsWith("fn="))?.Replace("fn=", "");
            var how = primary == null
                ? (ipv6 != null ? "SKIPPED (IPv6-only, IPv4 not yet recovered)" : "SKIPPED (no address)")
                : ipv4 != null ? primary
                : $"{primary} (IPv4 recovered from {recoverySource})";
            logger?.Log($"mDNS [{source}][{e.Announcement.Type}] fn='{fnDbg}' addrs=[{string.Join(", ", addresses)}] -> {how}");
            if (primary == null)
                return;

            if (!addresses.Contains(primary))
                addresses.Add(primary);

            var discoveredDevice = new DiscoveredDevice
            {
                IPAddress = primary,
                Addresses = addresses,
                Protocol = e.Announcement.Type,
                Port = e.Announcement.Port,
                Name = (e.Announcement.Txt.Where(x => x.ToString().StartsWith("fn=")).FirstOrDefault()?.Replace("fn=", ""))!,
                Headers = JsonSerializer.Serialize(e.Announcement.Txt),
                Usn = e.Announcement.Hostname,
                Id = id!,
            };

            if (discoveredDevice.Name != null
                && discoveredDevice.Usn != null
                && discoveredDevice.Headers != null
                && (discoveredDevice.Protocol.IndexOf(serviceType) >= 0
                    || discoveredDevice.Protocol.IndexOf(serviceTypeEmbedded) >= 0))
            {
                // Announcements arrive on Tmds.MDns's own threads while the drain timer reads the queue.
                // The reader has always locked; the writer did not, so the list could be resized underneath it.
                lock (discoveredDevices)
                {
                    discoveredDevices.Add(discoveredDevice);
                }
            }
        }

        /// <summary>
        /// Call the callback function for the discovered devices.
        /// </summary>
        private void OnAddDevice(object? sender, ElapsedEventArgs e)
        {
            if (discoveredDevices == null)
                return;

            lock (discoveredDevices)
            {
                if (discoveredDevices.Count > 0)
                {
                    var discoveredDevice = discoveredDevices[0];
                    onDiscovered(discoveredDevice);
                    discoveredDevices.RemoveAt(0);
                }
            }
        }

        public void Dispose()
        {
            timer?.Close();
            timer?.Dispose();
        }
    }

    internal class MsdnIps
    {
        public DateTime Added { get; set; }
        public IPEndPoint Endpoint { get; set; } = null!;
    }
}