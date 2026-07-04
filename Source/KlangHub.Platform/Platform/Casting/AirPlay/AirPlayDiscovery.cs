using System;
using System.Collections.Generic;
using KlangHub.Platform.Casting.Shared;

namespace KlangHub.Platform.Casting.AirPlay
{
    /// <summary>Rough classification of an AirPlay receiver, for the roadmap log (not modelled per-device).</summary>
    public enum AirPlayReceiverKind
    {
        Unknown,
        /// <summary>Legacy RAOP, unauthenticated AES (et=1) - streamable without HomeKit pairing.</summary>
        LegacyRaop,
        /// <summary>AirPlay 2 - transient HomeKit pairing + encrypted RTP required (HomePod, modern Apple TV).</summary>
        AirPlay2,
    }

    /// <summary>
    /// 2.2b-M3: discovers AirPlay receivers via mDNS (`_airplay._tcp` + `_raop._tcp`) and reports each as a
    /// neutral <see cref="CastDeviceDescriptor"/>. It also classifies each receiver (legacy RAOP vs AirPlay 2
    /// pairing-required) and logs it, so we learn what is actually on users' networks before committing to the
    /// (hard) AirPlay 2 sender. Sessions are deferred (see <see cref="AirPlayProvider"/>).
    /// </summary>
    public sealed class AirPlayDiscovery : IDeviceDiscovery
    {
        private static readonly string[] ServiceTypes = { "_airplay._tcp", "_raop._tcp" };
        private readonly MdnsDiscovery mdns;
        private readonly ILogger logger;

        public event EventHandler<CastDeviceDescriptor> DeviceDiscovered;

        public AirPlayDiscovery(MdnsDiscovery mdnsIn, ILogger loggerIn)
        {
            mdns = mdnsIn;
            logger = loggerIn;
            mdns.ServiceFound += OnServiceFound;
        }

        public void Start() => mdns.Start(ServiceTypes);

        public void Stop() => mdns.Stop();

        public void Dispose() => mdns.Dispose();

        private void OnServiceFound(MdnsService service)
        {
            var descriptor = ToDescriptor(service);
            if (descriptor == null)
                return;

            var model = service.Txt.TryGetValue("model", out var m) ? m : "?";
            logger?.Log($"Discovered AirPlay receiver '{descriptor.Name}' (kind={Classify(service)}, model={model})");
            DeviceDiscovered?.Invoke(this, descriptor);
        }

        /// <summary>Map an AirPlay/RAOP announcement to one descriptor: id from the stable deviceid (MAC),
        /// then pk, then address; a friendly name from the service instance (RAOP's "MAC@Name" -> "Name").</summary>
        internal static CastDeviceDescriptor ToDescriptor(MdnsService service)
        {
            if (service == null || string.IsNullOrEmpty(service.Address))
                return null;

            var id = Value(service.Txt, "deviceid") ?? Value(service.Txt, "pk") ?? service.Address;
            return new CastDeviceDescriptor(id, FriendlyName(service), ProviderId.AirPlay, false);
        }

        /// <summary>Classify by encryption/version TXT keys: srcvers or an encryption type >= 3
        /// (FairPlay/MFi) => AirPlay 2 pairing; only 0/1 => legacy unauthenticated RAOP.</summary>
        internal static AirPlayReceiverKind Classify(MdnsService service)
        {
            var txt = service.Txt;
            if (txt.ContainsKey("srcvers"))
                return AirPlayReceiverKind.AirPlay2;

            if (txt.TryGetValue("et", out var et) && !string.IsNullOrEmpty(et))
            {
                foreach (var part in et.Split(','))
                    if (int.TryParse(part.Trim(), out var n) && n >= 3)
                        return AirPlayReceiverKind.AirPlay2;
                return AirPlayReceiverKind.LegacyRaop;
            }
            return AirPlayReceiverKind.Unknown;
        }

        private static string FriendlyName(MdnsService service)
        {
            var instance = service.Instance ?? string.Empty;
            var at = instance.IndexOf('@');            // RAOP instance is "<MAC>@<name>"
            if (at >= 0 && at + 1 < instance.Length)
                instance = instance.Substring(at + 1);

            if (!string.IsNullOrEmpty(instance)) return instance;
            var model = Value(service.Txt, "model");
            if (model != null) return model;
            return !string.IsNullOrEmpty(service.HostName) ? service.HostName : service.Address;
        }

        private static string Value(IReadOnlyDictionary<string, string> txt, string key) =>
            txt.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
    }
}
