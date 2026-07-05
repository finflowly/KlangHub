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
        private Action<DiscoveredDevice> onDiscovered = null!;
        private List<DiscoveredDevice>? discoveredDevices;
        private Timer? timer;
        private List<MsdnIps>? msdnIps;
        private readonly ILogger? logger;

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
            discoveredDevices = new List<DiscoveredDevice>();
            timer = new Timer
            {
                Interval = 500,
                Enabled = true
            };
            timer.Elapsed += new ElapsedEventHandler(OnAddDevice);
            timer.Start();

            // MDNS search
            msdnIps = new List<MsdnIps>();
            MdnsSearch();
        }

        /// <summary>
        /// Do a scan on the network.
        /// </summary>
        public void MdnsSearch()
        {
            ServiceBrowser serviceBrowser = new ServiceBrowser();
            serviceBrowser.ServiceAdded += OnServiceAdded;
            serviceBrowser.ServiceRemoved += OnServiceRemoved;
            serviceBrowser.ServiceChanged += OnServiceChanged;
            serviceBrowser.StartBrowse(serviceType);

            ServiceBrowser serviceBrowserEmbedded = new ServiceBrowser();
            serviceBrowserEmbedded.ServiceAdded += OnServiceAdded;
            serviceBrowserEmbedded.ServiceRemoved += OnServiceRemoved;
            serviceBrowserEmbedded.ServiceChanged += OnServiceChanged;
            serviceBrowserEmbedded.StartBrowse(serviceTypeEmbedded);
        }

        /// <summary>
        /// Callback for when a device is changed.
        /// </summary>
        private void OnServiceChanged(object? sender, ServiceAnnouncementEventArgs e)
        {
            //TODO
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
            logger?.Log($"mDNS [{e.Announcement.Type}] fn='{fnDbg}' addrs=[{string.Join(", ", addresses)}] -> {how}");
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
                discoveredDevices.Add(discoveredDevice);
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