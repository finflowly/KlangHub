using System;
using System.Collections.Generic;
using System.Globalization;
using KlangHub.Core.Casting;
using KlangHub.Discover;

namespace KlangHub.Application
{
    /// <summary>
    /// Everything a Cast device can be made to tell us about itself, in one ordered list.
    ///
    /// Three sources, verified against all four devices on this network on 2026-09-04:
    ///   1. the mDNS TXT record  - model, friendly name, capability bits, protocol version, activity.
    ///      The ONLY place a speaker names its model. Always present.
    ///   2. eureka_info (:8008)  - firmware revision, language, uptime, update state, network, MAC.
    ///      Present on all four, but its "device_info" block (manufacturer, product name, hi-res and
    ///      multiroom capability flags) came back on ONE device out of four. Treated as a bonus.
    ///   3. DIAL device-desc.xml - manufacturer and model as UPnP. Answered by the television only;
    ///      the three audio devices return 404, because DIAL exists for TV apps.
    /// What no source gives, on any device: the room (that lives in Google's Home Graph), the serial number
    /// printed on the housing, and the list of codecs the device actually decodes. Codecs become readable
    /// only from inside our own receiver via canDisplayType() - see docs/PLAN-MULTIROOM-SYNC.md.
    ///
    /// Values that must be translated are returned as one of these tokens instead of prose:
    ///   Type          "tv" | "speaker" | "group"
    ///   HiResAudio,
    ///   Multiroom,
    ///   UpdatePending "yes" | "no"
    ///   Network       "ethernet" or the wifi network name
    ///   Uptime        seconds, invariant culture
    /// Everything else is a literal already fit to display.
    /// </summary>
    public static class DeviceFacts
    {
        public static IReadOnlyList<DeviceFact> For(DiscoveredDevice? device)
        {
            var facts = new List<DeviceFact>();
            if (device == null)
                return facts;

            var txt = CastTxt.Parse(device.Headers);
            var eureka = device.Eureka;
            var info = eureka?.DeviceInfo;

            string? Txt(string key) => txt.TryGetValue(key, out var v) && v.Length != 0 ? v : null;
            void Add(DeviceFactKind kind, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    facts.Add(new DeviceFact(kind, value!.Trim()));
            }

            Add(DeviceFactKind.Type, device.IsGroup
                ? DeviceFact.TypeGroup
                : CastCapabilities.Has(Txt("ca"), CastCapability.VideoOut)
                    ? DeviceFact.TypeTelevision
                    : DeviceFact.TypeSpeaker);

            Add(DeviceFactKind.Model, device.ModelName ?? Txt("md"));
            Add(DeviceFactKind.Manufacturer, info?.Manufacturer);

            // The full revision string ("1.68.cast_20250214_0202_RC06.726811595") says more than the build
            // number alone, so prefer it and fall back only when it is missing.
            Add(DeviceFactKind.Firmware,
                eureka?.Cast_build_revision ?? eureka?.BuildInfo?.Cast_build_revision ?? eureka?.Build_version);
            Add(DeviceFactKind.CastProtocol, Txt("ve"));

            // Not read from the device - nothing in the Cast sender protocol asks a receiver what it
            // decodes. These are the formats the Cast standard requires every receiver to accept, which is
            // why the view carries a footnote saying exactly that. Measuring the real list needs
            // canDisplayType() inside our own receiver; see docs/PLAN-MULTIROOM-SYNC.md.
            Add(DeviceFactKind.AudioFormats, "WAV/LPCM · FLAC · MP3 · AAC · Opus · Vorbis");

            if (info?.Capabilities != null)
            {
                Add(DeviceFactKind.HiResAudio, info.Capabilities.Hi_res_audio_supported ? DeviceFact.Yes : DeviceFact.No);
                Add(DeviceFactKind.Multiroom, info.Capabilities.Multizone_supported ? DeviceFact.Yes : DeviceFact.No);
            }

            var ssid = eureka?.Ssid ?? eureka?.Wifi?.Ssid;
            bool wired = (eureka?.Ethernet_connected ?? false) || (eureka?.Net?.Ethernet_connected ?? false);
            Add(DeviceFactKind.Network, wired ? DeviceFact.Ethernet : ssid);
            int signal = eureka?.Signal_level ?? eureka?.Wifi?.Signal_level ?? 0;
            if (!wired && signal != 0)
                Add(DeviceFactKind.Signal, signal.ToString(CultureInfo.InvariantCulture) + " dBm");

            Add(DeviceFactKind.Address, $"{device.IPAddress}:{device.Port}");
            Add(DeviceFactKind.MacAddress, RealMac(device.MACAddress) ?? RealMac(eureka?.Mac_address) ?? RealMac(info?.Mac_address));
            Add(DeviceFactKind.Serial, eureka?.Ssdp_udn ?? info?.Ssdp_udn ?? Txt("id"));
            Add(DeviceFactKind.Language, eureka?.Locale ?? eureka?.Settings?.Locale);

            double uptime = eureka?.Uptime ?? info?.Uptime ?? 0;
            if (uptime > 0)
                Add(DeviceFactKind.Uptime, uptime.ToString("F0", CultureInfo.InvariantCulture));

            if (eureka != null)
            {
                Add(DeviceFactKind.UpdatePending, eureka.Has_update ? DeviceFact.Yes : DeviceFact.No);
                Add(DeviceFactKind.ReleaseTrack, eureka.Release_track ?? eureka.BuildInfo?.Release_track);
            }

            Add(DeviceFactKind.Activity, Txt("rs"));
            return facts;
        }

        /// <summary>Cast devices that will not identify themselves announce an all-zero MAC; showing it would
        /// look like a real address. Anything else is passed through.</summary>
        private static string? RealMac(string? mac)
            => string.IsNullOrWhiteSpace(mac) || mac!.Replace(":", "").Replace("-", "").TrimStart('0').Length == 0
                ? null
                : mac.Trim();
    }
}
