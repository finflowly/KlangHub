using System;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using KlangHub.Classes;
using KlangHub.Core.Models;
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
        private readonly Orchestration.Orchestrator orchestrator;
        private NotifyIcon notifyIcon = null!;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ToolStripMenuItem> deviceMenuItems = new();
        private readonly Orchestration.SettingsService settingsService;
        private string Culture = null!;
        private readonly ILogger logger;
        private Size defaultSize = new Size(850, 550);

        private readonly Platform.NowPlaying.NowPlayingService nowPlaying;
        private readonly Platform.NowPlaying.RadioCoverService radioCovers;
        private string wantedCover = string.Empty;

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
            nowPlaying = new Platform.NowPlaying.NowPlayingService(loggerIn.Log);
            radioCovers = new Platform.NowPlaying.RadioCoverService(loggerIn.Log);
            orchestrator = new Orchestration.Orchestrator(devicesIn, streamingRequestListenerIn, castProviderIn, deviceStatusTimerIn, loggerIn);
            orchestrator.DeviceAdded += OnDeviceAdded;
            orchestrator.DeviceRemoved += OnRemoveDevice;
        }

        public void Initialize()
        {
            AddNotifyIcon();
            LoadSettings();
            configuration.Load(ApplyConfiguration, logger);
            orchestrator.Start(mainForm.RestartRecording);
            StartNowPlaying();
        }

        public void OnStreamingRequestConnect(Socket socketIn, string httpRequestIn)
            => orchestrator.OnStreamingRequestConnect(socketIn, httpRequestIn);

        public void OnRecordingDataAvailable(AudioFrame frame) => orchestrator.OnRecordingDataAvailable(frame);

        public void ClearMp3Buffer() => orchestrator.ClearMp3Buffer();

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

        public void SetDependencies(IMainForm mainFormIn)
        {
            mainForm = mainFormIn;
        }

        public void OnSetAutoRestart(bool autoRestartIn) => orchestrator.SetAutoRestart(autoRestartIn);

        public bool GetAutoRestart() => orchestrator.GetAutoRestart();

        public void ChangeIPAddressUsed(IPAddress ipAddressIn) => orchestrator.ChangeIPAddressUsed(ipAddressIn);

        public void SetStreamFormat(SupportedStreamFormat formatIn) => orchestrator.SetStreamFormat(formatIn);

        public void SetCulture(string cultureIn)
        {
            Culture = cultureIn;
        }

        public void ScanForDevices() => orchestrator.ScanForDevices();

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
                mainForm.SetStreamFormat(settings.StreamFormat ?? RecommendedDefaults.StreamFormat);
                mainForm.SetCulture(settings.Culture ?? Classes.StartupOptions.Culture ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
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
                mainForm.SetExtraBufferInSeconds(settings.ExtraBufferInSeconds ?? RecommendedDefaults.ExtraBufferSeconds);
                mainForm.SetRecordingDeviceID(settings.RecordingDeviceID ?? null);
                mainForm.SetAutoMute(settings.AutoMute ?? false);
                mainForm.SetMinimizeToTray(settings.MinimizeToTray ?? false);
                mainForm.SetGroupByRoom(settings.GroupByRoom ?? false);
                mainForm.SetReceiverAppId(settings.ReceiverAppId ?? string.Empty);
                mainForm.SetConvertMultiChannelToStereo(settings.ConvertMultiChannelToStereo ?? false);
                mainForm.SetDarkMode(settings.DarkMode ?? true);
                mainForm.SetStreamTitle(settings.StreamTitle ?? Properties.Strings.ChromeCast_StreamTitle);
                mainForm.SetNowPlayingSettings(
                    settings.ReadFileTags ?? true,
                    settings.ReadWindowsNowPlaying ?? true,
                    settings.NowPlayingFilePath ?? string.Empty);
                settingsService.StartDeviceChecks(devices, orchestrator.StartTask);
            }
            catch (ConfigurationErrorsException ex)
            {
                File.Delete(((ConfigurationErrorsException)ex.InnerException!).Filename);
                Process.GetCurrentProcess().Kill();
            }
        }

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
            settings.GroupByRoom = mainForm.GetGroupByRoom();
            settings.ReceiverAppId = mainForm.GetReceiverAppId();
            settings.ConvertMultiChannelToStereo = mainForm.GetConvertMultiChannelToStereo();
            settings.DarkMode = mainForm.GetDarkMode();
            settings.StreamTitle = mainForm.GetStreamTitle();
            settings.ReadFileTags = mainForm.GetReadFileTags();
            settings.ReadWindowsNowPlaying = mainForm.GetReadWindowsNowPlaying();
            settings.NowPlayingFilePath = mainForm.GetNowPlayingFilePath();

            settingsService.Save();
        }

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
            settings.StreamFormat = RecommendedDefaults.StreamFormat;
            settings.Culture = MainForm.SupportedCultures.Contains(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
                ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                : "en";
            settings.LogDeviceCommunication = false;
            settings.ShowLagControl = false;
            settings.LagControlValue = 1000;
            settings.StartApplicationWhenWindowsStarts = false;
            settings.FilterDevices = FilterDevicesEnum.ShowAll;
            settings.Size = defaultSize;
            settings.Left = Screen.PrimaryScreen!.Bounds.Width / 2 - settings.Size.Value.Width / 2;
            settings.Top = Screen.PrimaryScreen.Bounds.Height / 2 - settings.Size.Value.Height / 2;
            settings.ExtraBufferInSeconds = RecommendedDefaults.ExtraBufferSeconds;
            settings.RecordingDeviceID = null!;
            settings.AutoMute = false;
            settings.MinimizeToTray = false;
            settings.GroupByRoom = false;
            settings.ReceiverAppId = string.Empty;
            settings.ReadFileTags = true;
            settings.ReadWindowsNowPlaying = true;
            settings.NowPlayingFilePath = string.Empty;
            settings.ConvertMultiChannelToStereo = false;
            settings.DarkMode = true;
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
            mainForm.SetReceiverAppId(settings.ReceiverAppId);
            mainForm.SetNowPlayingSettings(
                settings.ReadFileTags!.Value, settings.ReadWindowsNowPlaying!.Value, settings.NowPlayingFilePath);
            mainForm.SetMinimizeToTray(settings.MinimizeToTray.Value);
            mainForm.SetGroupByRoom(settings.GroupByRoom ?? false);
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

        public string GetStreamingUrl() => orchestrator.GetStreamingUrl();

        public void CloseApplication()
        {
            SaveSettings();
            Dispose(true);
        }

        private bool trayHintShown;

        public void NotifyMinimizedToTray()
        {
            if (trayHintShown || notifyIcon == null)
                return;

            trayHintShown = true;
            try
            {
                notifyIcon.ShowBalloonTip(
                    7000,
                    Properties.Strings.Tray_StillRunning_Title,
                    Properties.Strings.Tray_StillRunning_Text,
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                logger.Log(ex, "ApplicationLogic.NotifyMinimizedToTray");
            }
        }

        public void SetLagThreshold(int lagThresholdIn) => orchestrator.SetLagThreshold(lagThresholdIn);

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            devices?.Dispose();
            orchestrator?.StopAndDisposeStreaming();
            NativeMethods.StopSetWindowsHooks();
            if (notifyIcon != null) notifyIcon.Visible = false;
            notifyIcon?.Dispose();
            mainForm?.Dispose();
            nowPlaying?.Dispose();
            radioCovers?.Dispose();
            orchestrator?.DisposeTaskList();
        }

        public bool WasPlaying(DiscoveredDevice discoveredDevice) => settingsService.WasPlaying(discoveredDevice);

        public void SetStreamTitle(string title) => orchestrator.SetStreamTitle(title);

        public string GetStreamTitle() => orchestrator.GetStreamTitle();

        public CastMediaMetadata GetStreamMediaInfo()
        {
            var track = nowPlaying.Cascade.Current;

            var title = track.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = orchestrator.GetStreamTitle();
            if (string.IsNullOrWhiteSpace(title))
                title = "KlangHub";

            var subtitle = track.Artist;
            if (string.IsNullOrWhiteSpace(subtitle))
                subtitle = Properties.Strings.Media_Subtitle ?? string.Empty;

            var album = track.Album;
            if (string.IsNullOrWhiteSpace(album))
                album = "KlangHub";

            return new CastMediaMetadata
            {
                Title = title,
                Subtitle = subtitle,
                Album = album,
                ImageUrl = StageCoverUrl() ?? string.Empty,
                ContentType = StreamCodec.ContentType(orchestrator.GetStreamFormat())
            };
        }

        private void StartNowPlaying()
        {
            nowPlaying.Updated += OnNowPlayingUpdated;
            radioCovers.Found += OnRadioCoverFound;
            nowPlaying.Start(settingsService.GetNowPlayingOptions());
        }

        private void OnNowPlayingUpdated(object? sender, Core.NowPlaying.NowPlayingUpdate update)
        {
            if (update.IsNewTrack)
                logger.Log($"now-playing: {update.Track.Artist ?? "(unknown artist)"} - {update.Track.Title ?? "(unknown title)"}");

            AskAboutCover(update.Track);
            PushToStages(update.Track, update.IsNewTrack);
        }

        private void AskAboutCover(Core.NowPlaying.NowPlayingTrack track)
        {
            var question = Core.NowPlaying.RadioCoverQuestion.For(track, nowPlaying.Cover != null);
            radioCovers.Ask(question);
            wantedCover = question.Worth ? question.Key : string.Empty;
        }

        private void OnRadioCoverFound(object? sender, Platform.NowPlaying.RadioCover cover)
        {
            if (!string.Equals(cover.Key, wantedCover, StringComparison.Ordinal))
                return;

            PushToStages(nowPlaying.Cascade.Current, isNewTrack: false);
        }

        private string? StageCoverUrl()
            => Core.NowPlaying.StageCover.Url(orchestrator.GetArtworkUrl(),
                                              Platform.NowPlaying.CurrentCover.Fingerprint,
                                              radioCovers.Known(wantedCover));

        public Core.NowPlaying.StageUpdate? GetStageUpdate()
            => Core.NowPlaying.StageUpdate.For(nowPlaying.Cascade.Current, zone: null,
                                               coverUrl: StageCoverUrl(), isNewTrack: true);

        private void PushToStages(Core.NowPlaying.NowPlayingTrack track, bool isNewTrack)
        {
            var update = Core.NowPlaying.StageUpdate.For(track, zone: null, coverUrl: StageCoverUrl(), isNewTrack);
            if (update == null)
                return;

            logger.Log(update.Describe());
            devices.SendStageUpdate(update);
        }

        public void ApplyNowPlayingOptions()
        {
            nowPlaying.Start(settingsService.GetNowPlayingOptions());
        }

        #region private helpers

        private void AddNotifyIcon()
        {
            try
            {
                var contextMenuStrip = new ContextMenuStrip();
                var menuItem = new ToolStripMenuItem
                {
                    Text = Properties.Strings.TrayIcon_Close
                };
                menuItem.Click += new EventHandler(CloseApplication);
                contextMenuStrip.Items.AddRange(new ToolStripMenuItem[] { menuItem });

                notifyIcon = new NotifyIcon();
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
                notifyIcon.Icon = Classes.Theme.LoadTrayIcon() ?? (Icon?)resources.GetObject("$this.Icon");
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
                                Port = 8009
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(ex, "ApplicationLogic.ApplyConfiguration");
            }
        }

        private void CloseApplication(object? sender, EventArgs e)
        {
            CloseApplication();
        }

        public void StartTask(Action action, CancellationTokenSource? cancellationTokenSource = null)
            => orchestrator.StartTask(action, cancellationTokenSource);

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
