using System;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using KlangHub.Classes;
using KlangHub.Platform.Audio;
using KlangHub.Application.Interfaces;
using KlangHub.Streaming.Interfaces;
using KlangHub.Discover.Interfaces;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using KlangHub.Discover;
using System.Threading;
using KlangHub.Rest;
using System.Configuration;
using System.IO;
using System.Diagnostics;
using KlangHub.Streaming;

namespace KlangHub.Application
{
    public class ApplicationLogic : IApplicationLogic, IDisposable
    {
        private readonly IDevices devices;
        private IMainForm mainForm = null!;
        private readonly IConfiguration configuration;
        private readonly IStreamingRequestsListener streamingRequestListener;
        private readonly IDiscoverDevices discoverDevices;
        private readonly IDeviceStatusTimer deviceStatusTimer;
        private readonly ICastProvider castProvider;
        // 2.2b-H4b: the WinForms-free orchestration (streaming pipeline, listener lifecycle, discovery
        // start, task runner, ICastHost). ApplicationLogic is now a thin tray shell that delegates here.
        private readonly Orchestration.Orchestrator orchestrator;
        private NotifyIcon notifyIcon = null!;
        // 2.2b-H3a: ApplicationLogic owns the per-device tray menu items (moved off IDevice/Device so
        // IDevice becomes WinForms-free). Keyed by device id; add/remove run on different threads.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ToolStripMenuItem> deviceMenuItems = new();
        // 2.2b-H4b-3: neutral settings persistence/merge (owns the UserSettings); the shell keeps the UI mapping.
        private readonly Orchestration.SettingsService settingsService;
        private string Culture = null!;
        private readonly ILogger logger;
        private Size defaultSize = new Size(850, 550);

        public ApplicationLogic(IDevices devicesIn, IDiscoverDevices discoverDevicesIn
            , IConfiguration configurationIn
            , IStreamingRequestsListener streamingRequestListenerIn, IDeviceStatusTimer deviceStatusTimerIn
            , ILogger loggerIn, ICastProvider castProviderIn)
        {
            devices = devicesIn;
            discoverDevices = discoverDevicesIn;
            configuration = configurationIn;
            streamingRequestListener = streamingRequestListenerIn;
            deviceStatusTimer = deviceStatusTimerIn;
            logger = loggerIn;
            castProvider = castProviderIn;
            settingsService = new Orchestration.SettingsService(loggerIn);
            orchestrator = new Orchestration.Orchestrator(devicesIn, streamingRequestListenerIn, castProviderIn, deviceStatusTimerIn, loggerIn);
            // 2.2b-H4b-2: the orchestrator owns the device add/remove flow and raises neutral events; the
            // tray shell reacts here (create/remove menu items + mainForm add/remove).
            orchestrator.DeviceAdded += OnDeviceAdded;
            orchestrator.DeviceRemoved += OnRemoveDevice;
        }

        /// <summary>
        /// Initialize the application.
        /// </summary>
        public void Initialize()
        {
            AddNotifyIcon();
            LoadSettings();
            configuration.Load(ApplyConfiguration, logger);
            orchestrator.Start(mainForm.RestartRecording);
        }

        /// <summary>
        /// Callback for the StreamingRequestListener, a device has made a new streaming connection.
        /// </summary>
        /// <param name="socketIn">the connected socket</param>
        /// <param name="httpRequestIn">the HTTP headers, including the 'CAST-DEVICE-CAPABILITIES' header</param>
        public void OnStreamingRequestConnect(Socket socketIn, string httpRequestIn)
            => orchestrator.OnStreamingRequestConnect(socketIn, httpRequestIn);

        /// <summary>
        /// Callback for the loopback recorder, new audio data is captured.
        /// </summary>
        /// <param name="dataToSendIn">the audio data in wav format</param>
        /// <param name="formatIn">the wav format that's used</param>
        public void OnRecordingDataAvailable(AudioFrame frame) => orchestrator.OnRecordingDataAvailable(frame);

        /// <summary>
        /// Clear the audio data in the mp3 encoder.
        /// </summary>
        public void ClearMp3Buffer() => orchestrator.ClearMp3Buffer();

        /// <summary>
        /// Callback for Devices, a new device is added.
        /// </summary>
        /// <param name="deviceIn">the new device</param>
        private void OnDeviceAdded(IDevice deviceIn)
        {
            if (deviceIn == null || mainForm == null)
                return;

            try
            {
                var menuItem = new ToolStripMenuItem
                {
                    Text = deviceIn.GetFriendlyName()
                };

                // 2.2b-4.4a: drive the tray per-device Play/Stop through the neutral casting session
                // (castProvider.CreateSession -> IPlaybackSession). The descriptor is captured now; the
                // session is re-resolved on each click. Falls back to the direct device path only if no
                // provider is present (does not happen in normal composition, but keeps the null-guard
                // style used by ScanForDevices).
                if (castProvider != null && deviceIn is IPlaybackSession playbackSession)
                {
                    var descriptor = playbackSession.Device;
                    menuItem.Click += (s, e) => orchestrator.TogglePlayStop(descriptor);
                }
                else
                {
                    menuItem.Click += (s, e) => deviceIn.OnClickPlayPause(s!, e);
                }

                notifyIcon?.ContextMenuStrip?.Items?.Insert(0, menuItem);
                if (deviceIn is IPlaybackSession menuSession)
                    deviceMenuItems[menuSession.Device.Id] = menuItem;
                SubscribeMenuChecked(deviceIn, menuItem);
                orchestrator.RequestDeviceStatus(deviceIn);
            }
            catch (Exception ex)
            {
                logger.Log(ex, "ApplicationLogic.OnAddDevice");
            }
            mainForm.AddDevice(deviceIn);
        }

        /// <summary>
        /// 2.2b-4.7: a device was removed (disposed) - drop its tray menu item and its UI control.
        /// Called from Devices cleanup on the DeviceStatusTimer thread; each UI touch is marshalled.
        /// </summary>
        private void OnRemoveDevice(IDevice device)
        {
            if (device == null || !(device is IPlaybackSession session))
                return;

            var id = session.Device.Id;

            if (deviceMenuItems.TryRemove(id, out var menuItem) && menuItem != null)
            {
                var strip = notifyIcon?.ContextMenuStrip;
                if (strip != null)
                {
                    if (!strip.IsDisposed && strip.InvokeRequired)
                        strip.BeginInvoke(new Action(() => RemoveMenuItem(strip, menuItem)));
                    else
                        RemoveMenuItem(strip, menuItem);
                }
            }

            mainForm.RemoveDevice(id);
        }

        private static void RemoveMenuItem(ContextMenuStrip strip, ToolStripMenuItem item)
        {
            if (!strip.IsDisposed && strip.Items.Contains(item))
                strip.Items.Remove(item);
            item.Dispose();
        }

        /// <summary>
        /// 2.2b-4.6: the tray item's Checked follows playback via the neutral StateChanged (moved out of
        /// DeviceControl). Marshalled through the ContextMenuStrip (a Control) since the event may arrive
        /// off the UI thread. Not explicitly unsubscribed - the menu item lives ~process-long (bounded).
        /// </summary>
        private void SubscribeMenuChecked(IDevice deviceIn, ToolStripMenuItem menuItem)
        {
            if (!(deviceIn is IPlaybackSession session))
                return;

            session.StateChanged += (s, state) =>
            {
                bool isPlaying = state == Core.Casting.PlaybackState.Playing || state == Core.Casting.PlaybackState.Buffering;
                var strip = notifyIcon?.ContextMenuStrip;
                if (strip != null && !strip.IsDisposed && strip.InvokeRequired)
                    strip.BeginInvoke(new Action(() => { if (!menuItem.IsDisposed) menuItem.Checked = isPlaying; }));
                else if (!menuItem.IsDisposed)
                    menuItem.Checked = isPlaying;
            };
        }

        /// <summary>
        /// Set the dependencies.
        /// </summary>
        /// <param name="mainFormIn">the form</param>
        public void SetDependencies(IMainForm mainFormIn)
        {
            mainForm = mainFormIn;
        }

        /// <summary>
        /// The user changed the checkbox to automatically restart devices when closed.
        /// </summary>
        public void OnSetAutoRestart(bool autoRestartIn) => orchestrator.SetAutoRestart(autoRestartIn);

        /// <summary>
        /// Automaticaly restart devices y/n.
        /// </summary>
        public bool GetAutoRestart() => orchestrator.GetAutoRestart();

        /// <summary>
        /// The user changed the ip address in the user interface.
        /// Restart streaming using the new ip address.
        /// </summary>
        /// <param name="ipAddressIn">the selected ip address</param>
        public void ChangeIPAddressUsed(IPAddress ipAddressIn) => orchestrator.ChangeIPAddressUsed(ipAddressIn);

        /// <summary>
        /// The user changed the stream format in the user interface.
        /// Restart streaming in the new format.
        /// </summary>
        /// <param name="formatIn">the chosen format</param>
        public void SetStreamFormat(SupportedStreamFormat formatIn) => orchestrator.SetStreamFormat(formatIn);

        /// <summary>
        /// The user changed the language in the user interface.
        /// </summary>
        /// <param name="cultureIn">the chosen culture</param>
        public void SetCulture(string cultureIn)
        {
            Culture = cultureIn;
        }

        /// <summary>
        /// Search for new devices in the network.
        /// </summary>
        public void ScanForDevices() => orchestrator.ScanForDevices();

        /// <summary>
        /// Load and apply the settings.
        /// </summary>
        private void LoadSettings()
        {
            if (devices == null || mainForm == null)
                return;

            try
            {
                settingsService.Upgrade();
                var settings = settingsService.Settings;

                devices.SetSettings(settings);
                mainForm.SetAutoStart(settings.AutoStartDevices ?? false);
                mainForm.SetAutoRestart(settings.AutoRestart ?? false);
                mainForm.SetStartLastUsedDevices(settings.StartLastUsedDevices ?? false);
                mainForm.SetWindowVisibility(settings.ShowWindowOnStart ?? true);
                mainForm.SetKeyboardHooks(settings.UseKeyboardShortCuts ?? false);
                mainForm.SetIP4AddressUsed(settings.Ip4AddressUsed ?? string.Empty);
                mainForm.SetStreamFormat(settings.StreamFormat ?? SupportedStreamFormat.Mp3_320);
                mainForm.SetCulture(settings.Culture ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
                mainForm.SetLogDeviceCommunication(settings.LogDeviceCommunication ?? false);
                mainForm.SetLagValue(settings.LagControlValue ?? 1000);
                mainForm.SetStartApplicationWhenWindowsStarts(settings.StartApplicationWhenWindowsStarts ?? false);
                mainForm.SetFilterDevices(settings.FilterDevices ?? FilterDevicesEnum.ShowAll);
                if (settings.Size == null || settings.Size.Value.Width < 50 || settings.Size.Value.Height < 50)
                    settings.Size = defaultSize;
                mainForm.SetSize(settings.Size.Value);
                mainForm.SetPosition(
                        Math.Min(Math.Max(settings.Left!.Value, 0), Screen.PrimaryScreen!.Bounds.Width),
                        Math.Min(Math.Max(settings.Top!.Value, 0), Screen.PrimaryScreen.Bounds.Height)
                    );
                mainForm.SetExtraBufferInSeconds(settings.ExtraBufferInSeconds ?? 4);
                mainForm.SetRecordingDeviceID(settings.RecordingDeviceID ?? null);
                mainForm.SetAutoMute(settings.AutoMute ?? false);
                mainForm.SetMinimizeToTray(settings.MinimizeToTray ?? false);
                mainForm.SetConvertMultiChannelToStereo(settings.ConvertMultiChannelToStereo ?? false);
                mainForm.SetDarkMode(settings.DarkMode ?? false);
                mainForm.SetStreamTitle(settings.StreamTitle ?? Properties.Strings.ChromeCast_StreamTitle);
                settingsService.StartDeviceChecks(devices, orchestrator.StartTask);
            }
            catch (ConfigurationErrorsException ex)
            {
                // Corrupted config file, remove the config file.
                File.Delete(((ConfigurationErrorsException)ex.InnerException!).Filename);
                Process.GetCurrentProcess().Kill();
            }
        }

        /// <summary>
        /// Save the settings.
        /// </summary>
        public void SaveSettings()
        {
            if (devices == null || mainForm == null)
                return;

            settingsService.MergeDiscoveredHosts(devices);
            var settings = settingsService.Settings;
            settings.StreamFormat = orchestrator.GetStreamFormat();
            settings.UseKeyboardShortCuts = mainForm.GetUseKeyboardShortCuts();
            settings.AutoStartDevices = mainForm.GetAutoStartDevices();
            settings.AutoRestart = mainForm.GetAutoRestart();
            settings.StartLastUsedDevices = mainForm.GetStartLastUsedDevices();
            settings.ShowWindowOnStart = mainForm.GetShowWindowOnStart();
            settings.Ip4AddressUsed = mainForm.GetIP4AddressUsed();
            settings.Culture = Culture;
            settings.LogDeviceCommunication = mainForm.GetLogDeviceCommunication();
            settings.ShowLagControl = mainForm.GetShowLagControl();
            settings.LagControlValue = mainForm.GetLagValue();
            settings.StartApplicationWhenWindowsStarts = mainForm.GetStartApplicationWhenWindowsStarts();
            settings.FilterDevices = mainForm.GetFilterDevices();
            settings.Size = mainForm.GetSize();
            settings.Left = mainForm.GetLeft();
            settings.Top = mainForm.GetTop();
            settings.ExtraBufferInSeconds = mainForm.GetExtraBufferInSeconds();
            settings.RecordingDeviceID = mainForm.GetRecordingDeviceID();
            settings.AutoMute = mainForm.GetAutoMute();
            settings.MinimizeToTray = mainForm.GetMinimizeToTray();
            settings.ConvertMultiChannelToStereo = mainForm.GetConvertMultiChannelToStereo();
            settings.DarkMode = mainForm.GetDarkMode();
            settings.StreamTitle = mainForm.GetStreamTitle();

            settingsService.Save();
        }

        /// <summary>
        /// Reset to the deafult setting.
        /// </summary>
        public void ResetSettings()
        {
            if (devices == null || mainForm == null)
                return;

            var settings = settingsService.Settings;
            settings.ChromecastDiscoveredDevices = new List<DiscoveredDevice>();
            settings.UseKeyboardShortCuts = false;
            settings.AutoStartDevices = false;
            settings.StartLastUsedDevices = false;
            settings.ShowWindowOnStart = true;
            settings.AutoRestart = false;
            settings.Ip4AddressUsed = string.Empty;
            settings.StreamFormat = SupportedStreamFormat.Mp3_320;
            settings.Culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            settings.LogDeviceCommunication = false;
            settings.ShowLagControl = false;
            settings.LagControlValue = 1000;
            settings.StartApplicationWhenWindowsStarts = false;
            settings.FilterDevices = FilterDevicesEnum.ShowAll;
            settings.Size = defaultSize;
            settings.Left = Screen.PrimaryScreen!.Bounds.Width / 2 - settings.Size.Value.Width / 2;
            settings.Top = Screen.PrimaryScreen.Bounds.Height / 2 - settings.Size.Value.Height / 2;
            settings.ExtraBufferInSeconds = 4;
            settings.RecordingDeviceID = null!;
            settings.AutoMute = false;
            settings.MinimizeToTray = false;
            settings.ConvertMultiChannelToStereo = false;
            devices.SetSettings(settings);
            mainForm.SetAutoStart(settings.AutoStartDevices.Value);
            mainForm.SetStartLastUsedDevices(settings.StartLastUsedDevices.Value);
            mainForm.SetAutoRestart(settings.AutoRestart.Value);
            mainForm.SetWindowVisibility(settings.ShowWindowOnStart.Value);
            mainForm.SetKeyboardHooks(settings.UseKeyboardShortCuts.Value);
            mainForm.SetIP4AddressUsed(settings.Ip4AddressUsed);
            mainForm.SetStreamFormat(settings.StreamFormat.Value);
            mainForm.SetCulture(settings.Culture);
            mainForm.SetLogDeviceCommunication(settings.LogDeviceCommunication.Value);
            mainForm.SetLagValue(settings.LagControlValue.Value);
            mainForm.SetStartApplicationWhenWindowsStarts(settings.StartApplicationWhenWindowsStarts.Value);
            mainForm.SetFilterDevices(settings.FilterDevices.Value);
            mainForm.SetSize(settings.Size.Value);
            mainForm.SetExtraBufferInSeconds(settings.ExtraBufferInSeconds.Value);
            mainForm.SetRecordingDeviceID(settings.RecordingDeviceID);
            mainForm.SetAutoMute(settings.AutoMute.Value);
            mainForm.SetMinimizeToTray(settings.MinimizeToTray.Value);
            mainForm.SetConvertMultiChannelToStereo(settings.ConvertMultiChannelToStereo.Value);
            mainForm.SetDarkMode(settings.DarkMode!.Value);
            mainForm.SetStreamTitle(Properties.Strings.ChromeCast_StreamTitle);
            settingsService.Save();
            devices?.Dispose();
            if (notifyIcon?.ContextMenuStrip != null)
            {
                for (int i = notifyIcon.ContextMenuStrip.Items.Count - 2; i >= 0; i--)
                {
                    notifyIcon.ContextMenuStrip.Items[i].Dispose();
                }
            }
            ScanForDevices();
        }

        /// <summary>
        /// Get the streaming url.
        /// </summary>
        /// <returns>the url that can be used to open a stream</returns>
        public string GetStreamingUrl() => orchestrator.GetStreamingUrl();

        /// <summary>
        /// Close the application.
        /// </summary>
        public void CloseApplication()
        {
            SaveSettings();
            Dispose(true);
        }

        public void SetLagThreshold(int lagThresholdIn) => orchestrator.SetLagThreshold(lagThresholdIn);

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            devices?.Dispose();
            orchestrator?.StopAndDisposeStreaming();
            NativeMethods.StopSetWindowsHooks();
            if (notifyIcon != null) notifyIcon.Visible = false;
            notifyIcon?.Dispose();
            mainForm?.Dispose();
            orchestrator?.DisposeTaskList();
        }

        /// <summary>
        /// Was the device playing when the application was closed for the last time?
        /// </summary>
        /// <returns>true if the device was playing, or false</returns>
        public bool WasPlaying(DiscoveredDevice discoveredDevice) => settingsService.WasPlaying(discoveredDevice);

        public void SetStreamTitle(string title) => orchestrator.SetStreamTitle(title);

        public string GetStreamTitle() => orchestrator.GetStreamTitle();

        /// <summary>Full media metadata for the LOAD: a premium receiver screen (title/subtitle/album + branded
        /// full-bleed artwork served from our own HTTP server) plus the codec-aware MIME type.</summary>
        public CastMediaMetadata GetStreamMediaInfo()
        {
            var title = orchestrator.GetStreamTitle();
            if (string.IsNullOrWhiteSpace(title))
                title = "KlangHub";

            return new CastMediaMetadata
            {
                Title = title,
                Subtitle = Properties.Strings.Media_Subtitle ?? string.Empty,
                Album = "KlangHub",
                ImageUrl = orchestrator.GetArtworkUrl(),
                ContentType = StreamCodec.ContentType(orchestrator.GetStreamFormat())
            };
        }

        #region private helpers

        /// <summary>
        /// Add an icon to the systray, with a context menu for the devices.
        /// </summary>
        private void AddNotifyIcon()
        {
            try
            {
                var contextMenuStrip = new ContextMenuStrip();
                var menuItem = new ToolStripMenuItem
                {
                    //Index = 0, 
                    Text = Properties.Strings.TrayIcon_Close
                };
                menuItem.Click += new EventHandler(CloseApplication);
                contextMenuStrip.Items.AddRange(new ToolStripMenuItem[] { menuItem });

                notifyIcon = new NotifyIcon();
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
                notifyIcon.Icon = (Icon?)resources.GetObject("$this.Icon");
                notifyIcon.Visible = true;
                notifyIcon.Text = Properties.Strings.MainForm_Text;
                notifyIcon.ContextMenuStrip = contextMenuStrip;
                notifyIcon.Click += mainForm.ToggleFormVisibility;
            }
            catch (Exception ex)
            {
                logger.Log(ex, "ApplicationLogic.AddNotifyIcon");
            }
        }

        /// <summary>
        /// Apply the settings in the configuration file.
        /// </summary>
        /// <param name="ipAddressesDevicesIn">
        /// ip addresses & device names
        /// format: 192.168.0.1,DeviceName1;192.168.0.2,DeviceName2
        /// </param>
        private void ApplyConfiguration(string ipAddressesDevicesIn, string ignoreIpAddressesDevicesIn, bool showLagControl)
        {
            try
            {
                mainForm.ShowLagControl(showLagControl);
                devices.SetIgnoreIpAddresses(ignoreIpAddressesDevicesIn);
                if (!string.IsNullOrWhiteSpace(ipAddressesDevicesIn))
                {
                    var ipDevices = ipAddressesDevicesIn.Split(';');
                    foreach (var ipDevice in ipDevices)
                    {
                        var arrDevice = ipDevice.Split(',');
                        devices.OnDeviceAvailable(
                            new DiscoveredDevice
                            {
                                IPAddress = arrDevice[0],
                                Name = arrDevice[1],
                                Port = 8009 // Port = 8009, adding device groups via the config is not possible.
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(ex, "ApplicationLogic.ApplyConfiguration");
            }
        }

        /// <summary>
        /// Callback for the systray icon to close the application.
        /// </summary>
        private void CloseApplication(object? sender, EventArgs e)
        {
            CloseApplication();
        }

        /// <summary>
        /// Start an action in a new task.
        /// </summary>
        public void StartTask(Action action, CancellationTokenSource? cancellationTokenSource = null)
            => orchestrator.StartTask(action, cancellationTokenSource);

        /// <summary>
        /// 
        /// </summary>
        public void SetRecordingDevice(AudioCaptureDevice? recordingDevice)
        {
            if(recordingDevice == null)
            {
                notifyIcon.Text = $"{Properties.Strings.MainForm_Text}";
            }
            else
            {
                notifyIcon.Text = $"{Properties.Strings.MainForm_Text} - {recordingDevice.Name}";
            }
        }

        #endregion
    }
}
