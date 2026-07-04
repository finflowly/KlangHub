using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using KlangHub.Classes;
using KlangHub.Application.Interfaces;
using KlangHub.Discover;

namespace KlangHub.Application.Orchestration
{
    /// <summary>
    /// 2.2b-H4b-3: WinForms-free settings persistence + merge/model logic extracted from ApplicationLogic.
    /// Owns the UserSettings instance; the tray shell binds <see cref="Settings"/> to the UI (the ~50
    /// mainForm Set*/Get* + Screen clamping stay in the shell). Depends only on neutral contracts, so the
    /// interesting logic (merge, WasPlaying, device checks) is unit-testable without WinForms.
    /// </summary>
    public sealed class SettingsService
    {
        private UserSettings settings = new UserSettings();
        private readonly ILogger logger;

        public SettingsService(ILogger loggerIn)
        {
            logger = loggerIn;
        }

        public UserSettings Settings => settings;

        /// <summary>Upgrade the settings once. Throws ConfigurationErrorsException on a corrupt file; the
        /// caller decides how to recover (the shell keeps the delete-file + kill-process behaviour).</summary>
        public void Upgrade()
        {
            if (!settings.Upgraded ?? true)
            {
                settings.Upgrade();
                settings.Upgraded = true;
            }
        }

        public void Save() => settings.Save();

        /// <summary>Merge the currently discovered hosts into the saved device list (the SaveSettings loop).</summary>
        public void MergeDiscoveredHosts(IDevices devices)
        {
            var discoveredDevices = settings.ChromecastDiscoveredDevices ?? new List<DiscoveredDevice>();

            // Remove (old) entries of devices without a saved MAC address, and of groups without a saved ID.
            discoveredDevices = RemoveOldEntries(discoveredDevices);

            foreach (var host in devices.GetHosts())
            {
                if (host.IsGroup)
                {
                    var discoveredDevice = discoveredDevices.Where(x => x.Id == host.Id);
                    if (!discoveredDevice.Any())
                    {
                        discoveredDevices.Add(host);
                    }
                    else
                    {
                        if (host.DeviceState == Communication.DeviceState.ConnectError)
                        {
                            discoveredDevices.Remove(discoveredDevice.First());
                        }
                        else
                        {
                            discoveredDevice.First().Name = host.Name;
                            discoveredDevice.First().IPAddress = host.IPAddress;
                            discoveredDevice.First().Port = host.Port;
                            discoveredDevice.First().DeviceState = host.DeviceState;
                        }
                    }
                }
                else
                {
                    var discoveredDevice = discoveredDevices.Where(x => x.MACAddress == host.MACAddress);
                    if (!discoveredDevice.Any())
                    {
                        discoveredDevices.Add(host);
                    }
                    else
                    {
                        discoveredDevice.First().DeviceState = host.DeviceState;
                    }
                }
            }
            settings.ChromecastDiscoveredDevices = discoveredDevices;
        }

        /// <summary>Kick off the "is this saved device on?" checks (the LoadSettings loop).</summary>
        public void StartDeviceChecks(IDevices devices, Action<Action, CancellationTokenSource> startTask)
        {
            if (settings.ChromecastDiscoveredDevices == null)
                return;

            settings.ChromecastDiscoveredDevices = RemoveOldEntries(settings.ChromecastDiscoveredDevices);
            for (int i = 0; i < settings.ChromecastDiscoveredDevices.Count; i++)
            {
                startTask(DeviceInformation.CheckDeviceIsOn(settings.ChromecastDiscoveredDevices[i], devices.OnDeviceAvailable, logger), null);
            }
        }

        /// <summary>Was the device playing when the application was closed for the last time?</summary>
        public bool WasPlaying(DiscoveredDevice discoveredDevice)
        {
            if (settings.ChromecastDiscoveredDevices == null)
                return false;

            for (int i = 0; i < settings.ChromecastDiscoveredDevices.Count; i++)
            {
                if (settings.ChromecastDiscoveredDevices[i].Port == discoveredDevice.Port &&
                    settings.ChromecastDiscoveredDevices[i].Name == discoveredDevice.Name)
                {
                    return settings.ChromecastDiscoveredDevices[i].DeviceState == Communication.DeviceState.Playing ||
                        settings.ChromecastDiscoveredDevices[i].DeviceState == Communication.DeviceState.Buffering ||
                        settings.ChromecastDiscoveredDevices[i].DeviceState == Communication.DeviceState.LoadingMedia;
                }
            }

            return false;
        }

        private static List<DiscoveredDevice> RemoveOldEntries(List<DiscoveredDevice> discoveredDevices)
        {
            return discoveredDevices.Where(
                x => (x.IsGroup && !string.IsNullOrEmpty(x.Id)) || (!x.IsGroup && !string.IsNullOrEmpty(x.MACAddress))).ToList();
        }
    }
}
