using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using NAudio.Wave;
using KlangHub.Communication;
using KlangHub.Classes;
using KlangHub.Application.Interfaces;
using KlangHub.Discover;

namespace KlangHub.Application
{
    public class Devices : IDevices
    {
        private readonly List<IDevice> deviceList = new List<IDevice>();
        private Action<Device>? onAddDeviceCallback;
        private Action<IDevice>? onRemoveDeviceCallback;
        private bool AutoStart;
        private bool StartLastUsedDevices;
        private IMainForm mainForm = null!;
        private IApplicationLogic applicationLogic = null!;
        private readonly ApplicationBuffer applicationBuffer = new ApplicationBuffer();
        private readonly ILogger logger = null!;
        private bool isMuted;
        private List<string> ignoreIpAddresses = null!;

        public Devices()
        {

        }

        public Devices(Logger loggerIn)
        {
            logger = loggerIn;
        }

        /// <summary>
        /// A new device is discoverd. Add the device, or update if it already exists.
        /// </summary>
        /// <param name="discoveredDevice">the discovered device</param>
        public void OnDeviceAvailable(DiscoveredDevice discoveredDevice)
        {
            if (deviceList == null || discoveredDevice == null)
                return;

            if (discoveredDevice.Port == 0 || discoveredDevice.Port == 10001)
            {
                logger?.Log($"Discovery: ignoring '{discoveredDevice.Name}' ({discoveredDevice.IPAddress}) - port {discoveredDevice.Port}.");
                return;
            }

            if (ignoreIpAddresses.Contains(discoveredDevice.IPAddress))
                return;

            if (!discoveredDevice.AddedByDeviceInfo && !discoveredDevice.IsGroup)
            {
                logger?.Log($"Discovery: '{discoveredDevice.Name}' ({discoveredDevice.IPAddress}) - fetching eureka_info.");
                var mdnsId = discoveredDevice.Id;
                applicationLogic.StartTask(DeviceInformation.GetDeviceInformation(
                    discoveredDevice, e => SetDeviceInformation(e, mdnsId), () => AddFromMdnsFallback(discoveredDevice), logger!));
            }
            else
            {
                lock(deviceList)
                {
                    var existingDevice = GetDevice(discoveredDevice);
                    if (existingDevice == null)
                    {
                        var newDevice = new Device(logger, applicationLogic);
                        newDevice.Initialize(discoveredDevice, e => SetDeviceInformation(e), StopGroup, applicationLogic.StartTask, IsGroupStatusBlank, AutoMute);
                        deviceList.Add(newDevice);
                        logger?.Log($"Device added: '{discoveredDevice.Name}' ({discoveredDevice.IPAddress}:{discoveredDevice.Port}){(discoveredDevice.IsGroup ? " [group]" : string.Empty)}.");
                        onAddDeviceCallback?.Invoke(newDevice);

                        var wasPlaying = applicationLogic.WasPlaying(discoveredDevice);
                        if ((AutoStart && !newDevice.IsGroup()) || (StartLastUsedDevices && wasPlaying))
                            newDevice.ResumePlaying();
                    }
                    else
                    {
                        // Preserve the stable mDNS id= across the device's OWN eureka re-fetch (Flow 2 passes
                        // id=null): otherwise the re-fetch would wipe the tile's stored id, defeating the
                        // id-reconcile on a LATER DHCP move. Seen in the HW log as alternating `id=b3f62d38…`
                        // / `id=` lines for the same device.
                        if (string.IsNullOrEmpty(discoveredDevice.Id))
                            discoveredDevice.Id = existingDevice.GetDiscoveredDevice()?.Id!;
                        existingDevice.Initialize(discoveredDevice, e => SetDeviceInformation(e), StopGroup, applicationLogic.StartTask, IsGroupStatusBlank, AutoMute);
                    }
                }
            }
        }

        /// <summary>
        /// If device is a group, stop devices in the group.
        /// If device is a device, stop the groups the device is in.
        /// </summary>
        private void StopGroup(IDevice deviceIn)
        {
            if (deviceIn == null)
                return;

            if (deviceIn.IsGroup())
            {
                StopGroupDevices(deviceIn);
            }
            else
            {
                StopGroups(deviceIn, true);
            }
        }

        /// <summary>
        /// Stop all groups a device is in.
        /// </summary>
        private void StopGroups(IDevice deviceIn, bool change)
        {
            if (deviceList == null || deviceIn == null)
                return;

            var eurekaIn = deviceIn.GetEureka();
            if (eurekaIn == null || eurekaIn?.Multizone?.Groups == null)
                return;

            foreach (var group in eurekaIn.Multizone.Groups)
            {
                // Stop all devices in the group.
                foreach (var device in deviceList)
                {
                    if (group.Name == device.GetFriendlyName())
                    {
                        device.Stop(true && change);
                    }
                }
            }
        }

        /// <summary>
        /// Stop all devices in a group.
        /// </summary>
        private void StopGroupDevices(IDevice deviceIn)
        {
            if (deviceList == null || deviceIn == null)
                return;

            foreach (var device in deviceList)
            {
                // Check if this device is in the device-group that has to stop.
                var deviceEureka = device.GetEureka();
                if (deviceEureka == null || deviceEureka?.Multizone?.Groups == null)
                    continue;

                foreach (var group in deviceEureka.Multizone.Groups)
                {
                    if (group.Name == deviceIn.GetFriendlyName())
                    {
                        device.Stop(false);
                        StopGroups(device, false);
                    }
                }
            }
        }

        /// <summary>
        /// Get the device with the IP and port.
        /// </summary>
        /// <returns></returns>
        private IDevice? GetDevice(DiscoveredDevice discoveredDevice)
        {
            if (discoveredDevice == null)
                return null;

            if (discoveredDevice.IsGroup)
                return deviceList.FirstOrDefault(d => d.GetDiscoveredDevice()?.Id == discoveredDevice.Id);

            // Dedup by MAC only when it is a REAL MAC. Google TV / Android TV / several speakers report a
            // placeholder all-zeros MAC (00:00:00:00:00:00) in eureka - that is not an identity. Treating it
            // as a real MAC collapses every such device into one tile (Google TV + TCL TV + Enchant all merge,
            // hiding devices and misrouting casts to whichever eureka'd last). For no/placeholder MAC, dedup
            // by IP so each distinct device keeps its own tile.
            var mac = discoveredDevice.Eureka?.GetMacAddress();
            if (HasRealMac(mac))
                return deviceList.FirstOrDefault(d =>
                {
                    var dmac = d.GetDiscoveredDevice()?.Eureka?.GetMacAddress();
                    return HasRealMac(dmac) && dmac == mac;
                });
            // A device that changes IP (DHCP renew / power-cycle) keeps its stable mDNS id= but changes IP:port
            // (and even its IPv6 host) - so reconcile by that id FIRST, so the move updates the existing tile
            // instead of spawning a duplicate + orphaning the old (the old one then hammers reconnect forever =
            // the "6 tiles, one on Error" zombie). Guarded so it can NOT regress the placeholder-MAC 5-device
            // saga: only when the incoming carries an id= (else fall through to IP:port exactly as before), and
            // scoped to non-group tiles (a co-located group carries its own distinct id). Distinct devices have
            // distinct ids, so this never re-collapses Google TV / TCL / Enchant.
            if (!string.IsNullOrEmpty(discoveredDevice.Id))
            {
                var byId = deviceList.FirstOrDefault(d => !d.IsGroup()
                    && SameStableId(d.GetDiscoveredDevice(), discoveredDevice));
                if (byId != null)
                    return byId;
            }

            // No id match: dedup by IP:port, not IP alone. A speaker and the multizone group it HOSTS share an
            // IP - e.g. the Enchant at .154:8009 fronts the "the multi-room group" group at .154:32223. IP-only dedup
            // collapsed the speaker into the group (update branch, no onAddDeviceCallback) so it never got a
            // tile. IP:port keeps them distinct and matches ChromecastDeviceId.From, which already keys
            // placeholder-MAC devices on IP:port (so list-dedup and session-id agree).
            return deviceList.FirstOrDefault(d => SamePlaceholderEndpoint(d.GetDiscoveredDevice(), discoveredDevice));
        }

        /// <summary>A real, usable MAC identity - not empty and not the all-zeros placeholder some Cast
        /// devices (Google TV / Android TV) report in eureka_info.</summary>
        internal static bool HasRealMac(string? mac) =>
            !string.IsNullOrEmpty(mac) && mac != "00:00:00:00:00:00";

        /// <summary>Two placeholder-MAC (no real identity) discovered devices are the same tile only when BOTH
        /// IP and port match. A receiver at :8009 and the multizone group it hosts at :32223 share an IP but
        /// are distinct cast endpoints - IP alone wrongly merged them. Mirrors ChromecastDeviceId.From's
        /// IP:port fallthrough for placeholder MACs.</summary>
        internal static bool SamePlaceholderEndpoint(DiscoveredDevice? existing, DiscoveredDevice? incoming) =>
            existing != null && incoming != null
            && existing.IPAddress == incoming.IPAddress
            && existing.Port == incoming.Port;

        /// <summary>Two discovered devices are the same tile when they carry the same stable mDNS id= - the only
        /// identity that survives a DHCP IP change (the placeholder MAC is shared, the IP + IPv6 host both move).
        /// Only matches when the INCOMING advertises a non-empty id (a device without one falls back to IP:port).</summary>
        internal static bool SameStableId(DiscoveredDevice? existing, DiscoveredDevice? incoming) =>
            existing != null && incoming != null
            && !string.IsNullOrEmpty(incoming.Id)
            && existing.Id == incoming.Id;

        /// <summary>
        /// Callback for when the device information is collected.
        /// </summary>
        /// <param name="eurekaIn"></param>
        private void SetDeviceInformation(DeviceEureka eurekaIn, string? mdnsId = null)
        {
            // Log the mDNS id= too - it is the only stable identity across a DHCP move, and this line confirms
            // (on a HW run) that each placeholder-MAC device advertises a distinct, stable id for the reconcile.
            logger?.Log($"eureka: name='{eurekaIn?.GetName()}' ip={eurekaIn?.GetIpAddress()} mac={eurekaIn?.GetMacAddress()} id={mdnsId}");
            var discoveredDevice = new DiscoveredDevice
            {
                IPAddress = eurekaIn!.GetIpAddress(),
                MACAddress = eurekaIn.GetMacAddress(),
                Name = eurekaIn.GetName(),
                Port = 8009,
                Protocol = "",
                Usn = null!,
                IsGroup = false,
                AddedByDeviceInfo = true,
                // Carry the mDNS id= through the eureka boundary (it was dropped here before - the root cause of
                // the DHCP-move zombie tile) so GetDevice can reconcile a moved device to its existing tile.
                Id = mdnsId!,
                Eureka = eurekaIn
            };
            OnDeviceAvailable(discoveredDevice);
        }

        /// <summary>
        /// Fallback when a discovered device does not serve the :8008 eureka_info endpoint (TV-integrated
        /// Cast / soundbars / third-party devices). Add it from the mDNS announcement so it is still shown
        /// and castable over :8009 - other cast apps don't require eureka_info either. Reuses the normal add
        /// path via a minimal eureka carrying just the discovered name + IP.
        /// </summary>
        private void AddFromMdnsFallback(DiscoveredDevice discoveredDevice)
        {
            if (discoveredDevice == null || string.IsNullOrEmpty(discoveredDevice.IPAddress))
                return;

            logger?.Log($"Adding '{discoveredDevice.Name}' ({discoveredDevice.IPAddress}) from mDNS - no eureka_info.");
            SetDeviceInformation(new DeviceEureka { Name = discoveredDevice.Name, Ip_address = discoveredDevice.IPAddress }, discoveredDevice.Id);
        }

        /// <summary>
        /// Volume up for all devices.
        /// </summary>
        public void VolumeUp()
        {
            if (deviceList == null)
                return;

            foreach (var device in deviceList)
            {
                device.VolumeUp();
            }
        }

        /// <summary>
        /// Volume down for all devices.
        /// </summary>
        public void VolumeDown()
        {
            if (deviceList == null)
                return;

            foreach (var device in deviceList)
            {
                device.VolumeDown();
            }
        }

        /// <summary>
        /// Volume mute for all devices.
        /// </summary>
        public void VolumeMute()
        {
            if (deviceList == null)
                return;

            foreach (var device in deviceList)
            {
                device.VolumeMute();
            }
        }

        /// <summary>
        /// Stop for all devices. 
        /// </summary>
        /// <returns>true if one of the devices was playing, or false</returns>
        public void Stop(bool changeUserMode = false)
        {
            if (deviceList == null || applicationBuffer == null)
                return;

            foreach (var device in deviceList)
            {
                device.Stop(changeUserMode);
            }
            applicationBuffer.ClearBuffer();
        }

        /// <summary>
        /// Start all devices.
        /// </summary>
        public void Start()
        {
            if (deviceList == null)
                return;

            foreach (var device in deviceList)
            {
                device.Start();
            }
        }

        /// <summary>
        /// A device has made a new streaming connection. Add the connection to the right device.
        /// </summary>
        /// <param name="socket">the socket</param>
        /// <param name="httpRequest">the HTTP headers, including the 'CAST-DEVICE-CAPABILITIES' header</param>
        public void AddStreamingConnection(Socket socket, string httpRequest, SupportedStreamFormat streamFormatIn)
        {
            if (deviceList == null || socket == null || applicationBuffer == null)
                return;

            var remoteAddress = ((IPEndPoint)socket.RemoteEndPoint!).Address.ToString();
            foreach (var device in deviceList)
            {
                if (device.AddStreamingConnection(remoteAddress, socket, streamFormatIn))
                {
                    applicationBuffer.SendStartupBuffer(device, streamFormatIn);
                    break;
                }
            }
        }

        /// <summary>
        /// New audio data is available.
        /// </summary>
        /// <param name="dataToSend">the data</param>
        /// <param name="format">the wav format that's used</param>
        /// <param name="reduceLagThreshold">value for the lag control</param>
        /// <param name="streamFormat">the stream format</param>
        public void OnRecordingDataAvailable(byte[] dataToSend, AudioFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat)
        {
            if (deviceList == null || dataToSend == null || applicationBuffer == null)
                return;

            if (applicationBuffer.IsStartBufferSend())
            {
                foreach (var device in deviceList)
                {
                    device.OnRecordingDataAvailable(dataToSend, format, reduceLagThreshold, streamFormat);
                }
            }

            // Keep a buffer
            applicationBuffer.AddToBuffer(dataToSend, format, reduceLagThreshold, streamFormat);
        }

        /// <summary>
        /// Get the status of a device.
        /// Also used to cleanup disposed devices (groups).
        /// </summary>
        public void OnGetStatus()
        {
            if (deviceList == null)
                return;

            // Cleanup disposed devices first.
            try
            {
                lock(deviceList)
                {
                    for (int i = deviceList.Count - 1; i >= 0; i--)
                    {
                        if (deviceList[i].GetDeviceState() == DeviceState.Disposed)
                        {
                            onRemoveDeviceCallback?.Invoke(deviceList[i]);   // let the UI drop control + tray item
                            deviceList.RemoveAt(i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(ex, "Devices.OnGetStatus");
            }

            foreach (var device in deviceList)
            {
                device.OnGetStatus();
            }
        }

        /// <summary>
        /// Set the value to auto start a device right after it has been added.
        /// </summary>
        /// <param name="autoStartIn"></param>
        public void SetSettings(UserSettings settingsIn)
        {
            if (settingsIn == null)
                return;

            AutoStart = settingsIn.AutoStartDevices ?? false;
            StartLastUsedDevices = settingsIn.StartLastUsedDevices ?? false;
            applicationBuffer.SetExtraBufferInSeconds(settingsIn.ExtraBufferInSeconds ?? 10);
        }

        /// <summary>
        /// Dispose all devices.
        /// </summary>
        public void Dispose()
        {
            if (deviceList == null)
                return;

            try
            {
                Stop(true);
                for (int i = deviceList.Count - 1; i >= 0; i--)
                {
                    var device = deviceList[i];
                    device?.SetDeviceState(DeviceState.Disposed);
                    device?.Dispose();
                }
                deviceList.Clear();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set the callback for when a device is added.
        /// </summary>
        /// <param name="onAddDeviceCallbackIn"></param>
        public void SetCallback(Action<Device> onAddDeviceCallbackIn)
        {
            onAddDeviceCallback = onAddDeviceCallbackIn;
        }

        /// <summary>
        /// 2.2b-4.7: callback for when a device is removed (disposed), so the UI can drop the control +
        /// tray item. Fired from OnGetStatus cleanup on the DeviceStatusTimer thread.
        /// </summary>
        public void SetRemoveCallback(Action<IDevice> onRemoveDeviceCallbackIn)
        {
            onRemoveDeviceCallback = onRemoveDeviceCallbackIn;
        }

        /// <summary>
        /// Set the objects this class is depending on.
        /// </summary>
        /// <param name="mainFormIn">the main form</param>
        /// <param name="applicationLogicIn">the application logic</param>
        public void SetDependencies(IMainForm mainFormIn, IApplicationLogic applicationLogicIn)
        {
            mainForm = mainFormIn;
            applicationLogic = applicationLogicIn;
        }

        /// <summary>
        /// Return a list of all devices.
        /// </summary>
        public List<DiscoveredDevice> GetHosts()
        {
            if (deviceList == null)
                return new List<DiscoveredDevice>();

            var hosts = new List<DiscoveredDevice>();
            foreach (var device in deviceList)
            {
                hosts.Add(device.GetDiscoveredDevice());
            }
            return hosts;
        }

        /// <summary>
        /// Set the device buffer in seconds.
        /// </summary>
        /// <param name="bufferInSeconds">the buffer in seconds</param>
        public void SetExtraBufferInSeconds(int bufferInSeconds)
        {
            applicationBuffer.SetExtraBufferInSeconds(bufferInSeconds);
        }

        /// <summary>
        /// Check if the status text of all devices in a group is blank.
        /// </summary>
        /// <param name="deviceIn">the group device</param>
        private bool IsGroupStatusBlank(IDevice deviceIn)
        {
            if (deviceList == null || deviceIn == null)
                return true;

            // Check the status text of all devices in the group.
            foreach (var device in deviceList)
            {
                var eureka = device.GetEureka();
                if (eureka == null || eureka?.Multizone?.Groups == null)
                    return true;

                foreach (var group in eureka.Multizone.Groups)
                {
                    if (group.Name == deviceIn.GetFriendlyName())
                    {
                        var statusText = device.GetStatusText();
                        if (!device.IsStatusTextBlankCheck(statusText))
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Automute the system volume.
        /// Mute when one device is playng, Unmute when no one is playing.
        /// </summary>
        public void AutoMute(bool playing)
        {
            applicationLogic.StartTask(() =>
            {
                if (mainForm.GetAutoMute())
                {
                    if (playing)
                    {
                        SystemVolume.Mute(true, mainForm.GetHandle());
                        isMuted = true;
                    }
                    else if (!playing && !IsAnyDevicePlaying() && isMuted)
                    {
                        SystemVolume.Mute(false, mainForm.GetHandle());
                        isMuted = false;
                    }
                }
            });
        }

        private bool IsAnyDevicePlaying()
        {
            foreach (var device in deviceList)
            {
                if (device.GetUserMode() == UserMode.Playing)
                    return true;
            }

            return false;
        }

        public List<IDevice> GetDeviceList()
        {
            return deviceList;
        }

        public void SetIgnoreIpAddresses(string ignoreIpAddressesDevicesIn)
        {
            ignoreIpAddresses = new List<string>();
            if (!string.IsNullOrWhiteSpace(ignoreIpAddressesDevicesIn))
            {
                ignoreIpAddresses.AddRange(ignoreIpAddressesDevicesIn.Split(';'));
            }
        }
    }
}
