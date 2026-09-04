using System;
using System.Windows.Forms;
using KlangHub.Application;
using KlangHub.UserControls;
using KlangHub.Application.Interfaces;
using System.Threading.Tasks;
using KlangHub.Classes;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Drawing;
using KlangHub.Streaming.Interfaces;
using System.Text;
using KlangHub.Streaming;
using System.Net.Sockets;
using System.Text.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Drawing.Drawing2D;

namespace KlangHub
{
    public partial class MainForm : Form, IMainForm
    {
        private readonly IApplicationLogic applicationLogic = null!;
        private readonly IDevices devices = null!;
        private readonly ILogger logger = null!;
        private IPAddress? previousIpAddress;
        private readonly IAudioCaptureEngine captureEngine = null!;
        private readonly ICastProvider castProvider = null!;
        private Size windowSize;
        private readonly StringBuilder log = new StringBuilder();
        private string? previousRecordingDeviceID = null;
        private bool isSetRecordingDeviceID = false;
        private bool previousRecordingDeviceExists;
        private bool eventHandlerAdded;
        private bool isRecordingDeviceSelected;
        private AudioCaptureDevice? previousDefaultDevice;
        private readonly WavGenerator wavGenerator = null!;

        public MainForm(IApplicationLogic applicationLogicIn, IDevices devicesIn, IAudioCaptureEngine captureEngineIn, ILogger loggerIn, ICastProvider castProviderIn)
        {
            InitializeComponent();

            ApplyLocalization();
            captureEngine = captureEngineIn;
            castProvider = castProviderIn;
            applicationLogic = applicationLogicIn;
            devices = devicesIn;
            logger = loggerIn;
            logger.SetCallback(Log);
            devices.SetDependencies(this, applicationLogic);
            applicationLogic.SetDependencies(this);
            // 2.2b-H4a-3: keep the neutral host's stream title in sync with the UI, so ICastHost.GetStreamTitle
            // no longer reaches into WinForms (the load message reads the live title as before).
            txtStreamTitle.TextChanged += (s, e) => applicationLogic.SetStreamTitle(txtStreamTitle.Text);
            wavGenerator = new WavGenerator();
            previousRecordingDeviceExists = false;
            SetupPremiumChrome();
        }

        public MainForm()
        {
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (applicationLogic == null)
                return;

            Update();
            AddIP4Addresses();
            applicationLogic.Initialize();
            NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(AddressChangedCallback);
            cmbIP4AddressUsed.SelectedIndexChanged += CmbIP4AddressUsed_SelectedIndexChanged;
            cmbBufferInSeconds.SelectedIndexChanged += CmbBufferInSeconds_SelectedIndexChanged;

            Assembly assembly = Assembly.GetExecutingAssembly();
            // Assembly.Location is empty in single-file/published builds; read the version from metadata instead.
            // Shown as Major.Minor on a release ("1.0"), with the patch digit appended only when there is one
            // ("1.0.3") - the trailing ".0" the compiler pads in is noise on the about line.
            var av = assembly.GetName().Version;
            var appVersion = av == null ? string.Empty
                : av.Build > 0 ? $"{av.Major}.{av.Minor}.{av.Build}"
                : $"{av.Major}.{av.Minor}";
            FillStreamFormats();
            FillFilterDevices();
            lblVersion.Text = $"{Properties.Strings.Version} {appVersion}";
            applicationLogic.StartTask(() =>
            {
                CheckForNewVersion(appVersion);
            });
            captureEngine.DataAvailable += (s, frame) => applicationLogic.OnRecordingDataAvailable(frame);
            captureEngine.LevelSampled += (s, bytes) => ShowWavMeterValue(bytes);
            captureEngine.DevicesChanged += (s, args) => AddRecordingDevices(new List<AudioCaptureDevice>(args.Devices), args.DefaultDevice);
            captureEngine.Start(BuildCaptureSettings());
        }

        public IntPtr GetHandle()
        {
            return this.Handle;
        }

        private void ApplyLocalization()
        {
            Text = Properties.Strings.MainForm_Text;
            grpVolume.Text = string.Empty; // no group caption - replaced by the live room-summary text (SetupRoomSummary)
            btnVolumeUp.Text = Properties.Strings.Button_Up_Text;
            btnVolumeDown.Text = Properties.Strings.Button_Down_Text;
            btnVolumeMute.Text = Properties.Strings.Button_Mute_Text;
            grpDevices.Text = string.Empty; // the live summary row above the grid is the heading now
            btnScan.Text = Properties.Strings.Button_ScanAgain_Text;
            grpLag.Text = Properties.Strings.Group_Lag_Text;
            lblLagMin.Text = Properties.Strings.Label_MinimumLag_Text;
            lblLagMax.Text = Properties.Strings.Label_MaximumLag_Text;
            lblLagExperimental.Text = Properties.Strings.Label_LagExperimental_Text;
            tabPageMain.Text = Properties.Strings.Tab_Main_Text;
            tabPageOptions.Text = Properties.Strings.Tab_Options_Text;
            grpOptions.Text = Properties.Strings.Group_Options_Text;
            lblIpAddressUsed.Text = Properties.Strings.Label_IPAddressUsed_Text;
            lblDevice.Text = Properties.Strings.Label_RecordingDevice_Text;
            lblStreamFormat.Text = Properties.Strings.Label_StreamFormat_Text;
            chkHook.Text = Properties.Strings.Check_KeyboardShortcuts_Text;
            chkShowWindowOnStart.Text = Properties.Strings.Check_ShowWindowOnStart_Text;
            chkAutoStart.Text = Properties.Strings.Check_AutomaticallyStart_Text;
            chkAutoStartLastUsed.Text = Properties.Strings.Check_AutonaticallyStartLastUsed_Text;
            chkAutoRestart.Text = Properties.Strings.Check_AutomaticallyRestart_Text;
            chkShowLagControl.Text = Properties.Strings.Check_ShowLagControl_Text;
            chkStartApplicationWhenWindowsStarts.Text = Properties.Strings.Check_StartApplicationWhenWindowsStarts_Text;
            chkMinimizeToTray.Text = Properties.Strings.Check_MinimizeToTray_Text;
            chkConvertMultiChannelToStereo.Text = Properties.Strings.Check_ConvertMultiChannelToStereo_Text;
            btnResetSettings.Text = Properties.Strings.Button_ResetSetting_Text;
            tabPageLog.Text = Properties.Strings.Tab_Log_Text;
            btnClipboardCopy.Text = Properties.Strings.Button_ClipboardCopy_Text;
            lblLanguage.Text = Properties.Strings.Label_Language_Text;
            btnClearLog.Text = Properties.Strings.Button_ClearLog_Text;
            chkLogDeviceCommunication.Text = Properties.Strings.Check_LogDeviceCommunication_Text;
            chkAutoMute.Text = Properties.Strings.Check_AutoMute_Text;
            linkHelp.Text = Properties.Strings.Label_LinkHelp_Text;
            volumeMeterTooltip.SetToolTip(pnlVolumeMeter, Properties.Strings.Tooltip_RecordingLevel_Text);
            volumeMeterTooltip.SetToolTip(lblDb, Properties.Strings.Tooltip_RecordingLevel_Text);
            volumeMeterTooltip.SetToolTip(volumeMeter, Properties.Strings.Tooltip_RecordingLevel_Text);
            lblFilterDevices.Text = Properties.Strings.Label_FilterDevices_Text;
            RefreshTabCaptions();
            lblBufferInSeconds.Text = Properties.Strings.Label_BufferInSeconds_Text;
            lblStreamTitle.Text = Properties.Strings.Label_StreamTitle_Text;
            chkDarkMode.Text = Properties.Strings.Check_DarkMode_Text;

            FillLanguages();
        }

        /// <summary>
        /// The 24 official languages of the European Union. Each has its own Strings.&lt;code&gt;.resx, which the
        /// build turns into a satellite assembly; anything the translation misses falls back to English.
        /// </summary>
        internal static readonly string[] SupportedCultures =
        {
            "bg", "cs", "da", "de", "el", "en", "es", "et", "fi", "fr", "ga", "hr",
            "hu", "it", "lt", "lv", "mt", "nl", "pl", "pt", "ro", "sk", "sl", "sv",
        };

        /// <summary>One row of the language picker: the culture code plus the language's own name.</summary>
        private sealed class LanguageItem
        {
            public required string Code { get; init; }
            public required string Display { get; init; }
            public override string ToString() => Display;
        }

        /// <summary>
        /// Fills the language picker once. The entries are endonyms - a language is always listed under its
        /// own name ("Deutsch", "Español"), so the list never changes with the UI language and someone who
        /// picked the wrong one can still find their way back. The name comes from the translation when it
        /// carries one, otherwise from the OS culture data, so a language works the moment its file exists.
        /// </summary>
        private void FillLanguages()
        {
            if (cmbLanguage == null) return;

            if (cmbLanguage.Items.Count == 0)
            {
                var items = new List<LanguageItem>();
                foreach (var code in SupportedCultures)
                {
                    var ci = CultureInfo.GetCultureInfo(code);
                    items.Add(new LanguageItem { Code = code, Display = LanguageName(ci) });
                }
                items.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase));
                cmbLanguage.Items.AddRange(items.ToArray());
            }

            var current = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            int idx = IndexOfCulture(current);
            if (idx >= 0 && cmbLanguage.SelectedIndex != idx)
                cmbLanguage.SelectedIndex = idx;
            else if (cmbLanguage.SelectedIndex < 0)
                cmbLanguage.SelectedIndex = Math.Max(0, IndexOfCulture("en"));
        }

        private int IndexOfCulture(string code)
        {
            for (int i = 0; i < cmbLanguage.Items.Count; i++)
                if (cmbLanguage.Items[i] is LanguageItem li && li.Code == code) return i;
            return -1;
        }

        /// <summary>The language's own name: the translation's "Language" entry if it has one, else the OS
        /// endonym with a capital first letter (Windows lowercases several of them, e.g. "français").</summary>
        private static string LanguageName(CultureInfo ci)
        {
            var fromResx = Properties.Strings.ResourceManager.GetString("Language", ci);
            var neutral = Properties.Strings.ResourceManager.GetString("Language", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(fromResx) && (ci.TwoLetterISOLanguageName == "en" || fromResx != neutral))
                return fromResx!;

            var native = ci.NativeName;
            int paren = native.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) native = native[..paren];
            return native.Length > 0 ? char.ToUpper(native[0], ci) + native[1..] : ci.Name;
        }

        private void FillStreamFormats()
        {
            if (cmbStreamFormat == null)
                return;

            if (cmbStreamFormat.Items.Count == 0)
            {
                // WAV 24-bit first: uncompressed HiFi out-of-box default (the maintainer's setup runs it problem-free).
                // FLAC follows, still labelled "recommended" - it's the robust pick for small/weak speakers that
                // struggled with high-bitrate uncompressed LPCM (ERROR 102). MP3 last for legacy compatibility.
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Wav_24bit));
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Flac));
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Wav_16bit));
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Wav_32bit));
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Wav));
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Mp3_320));
                cmbStreamFormat.Items.Add(new ComboboxItem(SupportedStreamFormat.Mp3_128));
                cmbStreamFormat.SelectedIndex = 0; // WAV 24-bit
                SetStreamFormat();
            }
        }

        private void AddressChangedCallback(object? sender, EventArgs e)
        {
            AddIP4Addresses();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (applicationLogic == null)
                return;

            if (GetMinimizeToTray())
            {
                applicationLogic.SaveSettings();
                Hide();
                e.Cancel = true;
            }
            else
            {
                applicationLogic.CloseApplication();
            }
        }

        // 2.2b-4.4b: per-device session accessor for the control (volume/mute). Null when no provider is
        // wired (e.g. the parameterless designer ctor); the control then falls back to the direct device path.
        private Func<IPlaybackSession> BuildSessionAccessor(IDevice device)
        {
            if (castProvider == null || !(device is IPlaybackSession playbackSession))
                return null!;
            var descriptor = playbackSession.Device;                 // stable Id, captured now
            return () => castProvider.CreateSession(descriptor);     // re-resolved per volume action
        }

        // 2.2b-4.7: remove a device's control when the device is gone (mirrors AddDevice's marshaling).
        public void RemoveDevice(string id)
        {
            if (InvokeRequired) { Invoke(new Action<string>(RemoveDevice), new object[] { id }); return; }
            if (IsDisposed || pnlDevices == null)
                return;

            var dc = pnlDevices.Controls.OfType<DeviceControl>().FirstOrDefault(c => c.Id == id);
            if (dc != null)
            {
                dc.Hide();
                dc.Dispose();
            }
        }

        public void AddDevice(IDevice device)
        {
            if (device == null || pnlDevices == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<Device>(AddDevice), new object[] { device });
                return;
            }
            if (IsDisposed) return;

            var deviceControl = new DeviceControl(BuildSessionAccessor(device));
            deviceControl.RoomChanged += (s, e) => RebuildDeviceLayout();
            pnlDevices.Controls.Add(deviceControl);
            var filter = GetFilterDevices();
            if (filter != null)
                deviceControl.Visible = FilterDevices.ShowFilterDevices(deviceControl.IsGroup, filter.Value);

            // Sort alphabetically.
            var deviceName = device.GetFriendlyName();
            for (int i = 0; i < pnlDevices.Controls.Count - 1; i++)
            {
                if (pnlDevices.Controls[i] is DeviceControl recordingDevice)
                {
                    var name = recordingDevice.GetDeviceName();
                    if (string.CompareOrdinal(deviceName, name) < 0)
                    {
                        pnlDevices.Controls.SetChildIndex(deviceControl, i);
                        break;
                    }
                }
            }
            AutoSizeToDeviceCount();
        }

        public void ShowLagControl(bool showLag)
        {
            if (pnlDevices == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<bool>(ShowLagControl), new object[] { showLag });
                return;
            }
            if (IsDisposed) return;

            if (!showLag)
            {
                grpDevices.Height = tabPageMain.Height - grpVolume.Height - 30;
            }
            else
            {
                grpDevices.Height = tabPageMain.Height - grpVolume.Height - grpLag.Height - 30;
            }
            pnlDevices.Height = grpDevices.Height - 30;
            grpLag.Visible = showLag;
            chkShowLagControl.Checked = showLag;
        }

        public void SetLagValue(int lagValue)
        {
            if (trbLag == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<int>(SetLagValue), new object[] { lagValue });
                return;
            }
            if (IsDisposed) return;

            trbLag.Value = lagValue;
        }

        public void SetKeyboardHooks(bool useShortCuts)
        {
            if (chkHook == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<bool>(SetKeyboardHooks), new object[] { useShortCuts });
                return;
            }
            if (IsDisposed) return;

            chkHook.Checked = useShortCuts;
        }

        public void ToggleFormVisibility(object? sender, EventArgs e)
        {
            if (e.GetType().Equals(typeof(MouseEventArgs)))
            {
                if (((MouseEventArgs)e).Button != MouseButtons.Left) return;
            }

            if (Visible)
            {
                Hide();
            }
            else
            {
                Show();
                Activate();
                WindowState = FormWindowState.Normal;
            }
        }

        public void Log(string message)
        {
            if (txtLog == null || log == null)
                return;

            try
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string>(Log), new object[] { message });
                    return;
                }
                if (IsDisposed) return;

                if (message.Contains("\"type\":\"PONG\"") || message.Contains("\"type\":\"PING\""))
                {
                    lblPingPong.Text = $"{Properties.Strings.Label_KeepAlive_Text} {DateTime.Now.ToLongTimeString()}";
                    lblPingPong.Update();
                }
                else
                {
                    if (chkLogDeviceCommunication.Checked)
                    {
                        message += "\r\n\r\n";
                        txtLog.AppendText(message);
                        log.Append(message);

                        if (txtLog.Text.Length > 2000000)
                            txtLog.Clear();

                        if (log.Length > 100000000)
                            log.Clear();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void TrbLag_Scroll(object sender, EventArgs e)
        {
            if (applicationLogic == null)
                return;

            applicationLogic.SetLagThreshold(trbLag.Value);
        }

        private void ChkHook_CheckedChanged(object sender, EventArgs e)
        {
            if (chkHook == null)
                return;

            if (chkHook.Checked)
                NativeMethods.StartSetWindowsHooks(devices.VolumeUp, devices.VolumeDown, devices.VolumeMute);
            else
                NativeMethods.StopSetWindowsHooks();
        }

        private void BtnVolumeUp_Click(object sender, EventArgs e)
        {
            if (devices == null)
                return;

            devices.VolumeUp();
        }

        private void BtnVolumeDown_Click(object sender, EventArgs e)
        {
            if (devices == null)
                return;

            devices.VolumeDown();
        }

        private void BtnVolumeMute_Click(object sender, EventArgs e)
        {
            if (devices == null)
                return;

            devices.VolumeMute();
        }

        public async void SetWindowVisibility(bool visible)
        {
            if (chkShowWindowOnStart == null)
                return;

            chkShowWindowOnStart.Checked = visible;
            if (visible)
                Show();
            else
            {
                await HideWindow();
            }
        }

        private async Task HideWindow()
        {
            await Task.Delay(1000);
            Hide();
        }

        public void AddRecordingDevices(List<AudioCaptureDevice> devices, AudioCaptureDevice defaultdevice)
        {
            if (devices == null || cmbRecordingDevice == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<List<AudioCaptureDevice>, AudioCaptureDevice>(AddRecordingDevices), new object[] { devices, defaultdevice });
                return;
            }
            if (IsDisposed) return;

            // Remove items that have become unavailable.
            for (int i = cmbRecordingDevice.Items.Count - 1; i >= 0; i--)
            {
                var remove = true;
                foreach (var device in devices)
                {
                    if (((AudioCaptureDevice)cmbRecordingDevice.Items[i]!).Id == device.Id)
                    {
                        remove = false;
                    }
                }
                if (remove)
                {
                    cmbRecordingDevice.Items.RemoveAt(i);
                }
            }

            // Add items that don't occur in the combobox.
            foreach (var device in devices)
            {
                var exists = false;
                for (int i = 0; i < cmbRecordingDevice.Items.Count; i++)
                {
                    if (((AudioCaptureDevice)cmbRecordingDevice.Items[i]!).Id == device.Id)
                    {
                        exists = true;
                    }
                }
                if (!exists)
                {
                    var index = cmbRecordingDevice.Items.Add(device);
                }
            }

            // Select the new default device when the default device has changed.
            if (previousDefaultDevice != null)
            {
                var selectedDevice = (AudioCaptureDevice?)cmbRecordingDevice.SelectedItem;
                var nrSameDataflowItems = 0;
                for (int i = 0; i < cmbRecordingDevice.Items.Count; i++)
                {
                    var device = (AudioCaptureDevice)cmbRecordingDevice.Items[i]!;
                    if (device.Flow == selectedDevice?.Flow) nrSameDataflowItems++;
                }
                if (defaultdevice.Id != previousDefaultDevice.Id 
                    && (selectedDevice?.Flow == defaultdevice.Flow || nrSameDataflowItems <= 1))
                {
                    for (int i = 0; i < cmbRecordingDevice.Items.Count; i++)
                    {
                        var device = (AudioCaptureDevice)cmbRecordingDevice.Items[i]!;
                        if (device.Id == defaultdevice.Id)
                        {
                            if (cmbRecordingDevice.SelectedIndex != i)
                            {
                                cmbRecordingDevice.SelectedIndex = i;
                            }
                        }
                    }
                }
            }

            // Select the right device.
            if (!isRecordingDeviceSelected || !previousRecordingDeviceExists)
            {
                for (int i = 0; i < cmbRecordingDevice.Items.Count; i++)
                {
                    var device = (AudioCaptureDevice)cmbRecordingDevice.Items[i]!;
                    if (!isSetRecordingDeviceID && device.Id == defaultdevice.Id)
                    {
                        // Nothing previously selected, select the default device.
                        if (cmbRecordingDevice.SelectedIndex != i)
                        {
                            cmbRecordingDevice.SelectedIndex = i;
                            PlaySilence();
                            isRecordingDeviceSelected = true;
                        }
                        previousRecordingDeviceExists = true;
                    }
                }
                for (int i = 0; i < cmbRecordingDevice.Items.Count; i++)
                {
                    var device = (AudioCaptureDevice)cmbRecordingDevice.Items[i]!;
                    if (!isSetRecordingDeviceID && device.Id == previousRecordingDeviceID)
                    {
                        // Select the previously selected device (only once).
                        cmbRecordingDevice.SelectedIndex = i;
                        PlaySilence();
                        previousRecordingDeviceID = string.Empty;
                        previousRecordingDeviceExists = true;
                        isRecordingDeviceSelected = true;
                    }
                }
            }
            previousDefaultDevice = defaultdevice;
            isSetRecordingDeviceID = true;

            // Show recording device in the UI
            var selected = (AudioCaptureDevice?)cmbRecordingDevice.SelectedItem;
            if (selected?.Id != defaultdevice.Id)
            {
                Text = $"{Properties.Strings.MainForm_Text} - {selected!.Name}";
                applicationLogic.SetRecordingDevice(selected);
            }
            else
            {
                Text = $"{Properties.Strings.MainForm_Text}";
                applicationLogic.SetRecordingDevice(null);
            }

            if (!eventHandlerAdded)
            {
                cmbRecordingDevice.SelectedIndexChanged += CmbRecordingDevice_SelectedIndexChanged;
                eventHandlerAdded = true;
                // The combo was just populated and the saved/default device restored. Point the
                // engine at that device now: at Start() the combo was still empty, so the engine
                // is on its first-working fallback rather than the device the user actually wants.
                captureEngine.Apply(BuildCaptureSettings());
            }
        }



        private void CmbRecordingDevice_SelectedIndexChanged(object? sender, EventArgs e)
        {
            isRecordingDeviceSelected = true;
            captureEngine.Apply(BuildCaptureSettings());
            PlaySilence();
        }

        private AudioCaptureSettings BuildCaptureSettings()
        {
            var selected = cmbRecordingDevice?.SelectedItem as AudioCaptureDevice;
            return new AudioCaptureSettings((selected?.Id)!, GetSelectedStreamFormat(), GetConvertMultiChannelToStereo());
        }

        private void PlaySilence()
        {
            if (cmbRecordingDevice.SelectedItem != null)
            {
                wavGenerator.Stop();
                var device = (AudioCaptureDevice)cmbRecordingDevice.SelectedItem;
                wavGenerator.PlaySilenceLoop(device.Name, device.SampleRate, device.Channels);
            }
        }

        private void ChkAutoRestart_CheckedChanged(object sender, EventArgs e)
        {
            if (applicationLogic == null)
                return;

            applicationLogic.OnSetAutoRestart(chkAutoRestart.Checked);
        }

        public void SetAutoRestart(bool autoRestart)
        {
            if (chkAutoRestart == null)
                return;

            chkAutoRestart.Checked = autoRestart;
        }

        public void AddIP4Addresses()
        {
            if (cmbIP4AddressUsed == null)
                return;

            if (!InvokeRequired)
            {
                var oldAddressUsed = (IPAddress?)cmbIP4AddressUsed.SelectedItem;
                //LogNetworkInformation();
                var ip4Adresses = Network.GetIp4ddresses();

                //logger?.Log($"Add IP4 addresses: {string.Join(" - ", ip4Adresses.Select(x => x.IPAddress))}");
                cmbIP4AddressUsed.Items.Clear();
                if (ip4Adresses.Count > 0)
                {
                    foreach (var adapter in ip4Adresses)
                    {
                        cmbIP4AddressUsed.Items.Add(adapter.IPAddress!);
                    }

                    if (ip4Adresses.Any(x => x.IPAddress?.ToString() == oldAddressUsed?.ToString()))
                    {
                        cmbIP4AddressUsed.SelectedItem = oldAddressUsed;
                        previousIpAddress = oldAddressUsed;
                    }
                    else
                    {
                        var addressUsed = Network.GetIp4Address();
                        if (addressUsed != null)
                        {
                            cmbIP4AddressUsed.SelectedItem = addressUsed;
                            previousIpAddress = addressUsed;
                        }
                    }
                }
            }
        }

        private void LogNetworkInformation()
        {
            Log($"Network information:");
            Log($"");
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in networkInterfaces)
            {
                Log($"Name networkInterface: {networkInterface.Name}");
                var properties = networkInterface.GetIPProperties();
                if (properties != null)
                {
                    foreach (var ip in properties.UnicastAddresses)
                    {
                        if (ip != null && ip.Address != null)
                        {
                            var address = ip.Address;
                            if (address.ToString().Length >= 5)
                                Log($"Address: {address.ToString().Substring(0, 5)}**************");
                            else
                                Log($"Address: {address.ToString()}");
                            Log($"Address family: {address.AddressFamily.ToString()}");
                            Log($"Operational status: {networkInterface.OperationalStatus.ToString()}");
                            Log($"NetworkInterface type: {networkInterface.NetworkInterfaceType.ToString()}");
                            Log($"GatewayAddresses count: {properties.GatewayAddresses.Count.ToString()}");
                            if (address.AddressFamily == AddressFamily.InterNetwork
                                && Network.IsInLocalIpRange(address)
                                && networkInterface.OperationalStatus != OperationalStatus.Down
                                && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                && properties.GatewayAddresses.Count > 0)
                            {
                                var IsEthernet = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet3Megabit ||
                                                networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                                                networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
                                                networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT;
                                Log($"Is ethernet: {IsEthernet}");
                            }
                        }
                        Log($"");
                    }
                    Log($"_______________________________________________________________________");
                    Log($"");
                }
            }
        }

        private void CmbIP4AddressUsed_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbIP4AddressUsed == null)
                return;

            var ipAddress = (IPAddress?)cmbIP4AddressUsed.SelectedItem;
            if (ipAddress?.ToString() != previousIpAddress?.ToString())
                applicationLogic.ChangeIPAddressUsed(ipAddress!);
            previousIpAddress = ipAddress;
        }

        private void BtnClipboardCopy_Click(object sender, EventArgs e)
        {
            if (log == null)
                return;

            if (!string.IsNullOrEmpty(log.ToString()))
            {
                try
                {
                    Clipboard.SetText(log.ToString());
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\r\n\r\nPlease try again.");
                }
            }
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            if (applicationLogic == null)
                return;

            applicationLogic.ScanForDevices();
        }

        public void SetAutoStart(bool autoStart)
        {
            if (chkAutoStart == null)
                return;

            chkAutoStart.Checked = autoStart;
        }

        public bool GetUseKeyboardShortCuts()
        {
            if (chkHook == null)
                return false;

            return chkHook.Checked;
        }

        public bool GetAutoStartDevices()
        {
            if (chkAutoStart == null)
                return false;

            return chkAutoStart.Checked;
        }

        public bool GetShowWindowOnStart()
        {
            if (chkShowWindowOnStart == null)
                return false;

            return chkShowWindowOnStart.Checked;
        }

        public bool GetAutoRestart()
        {
            if (chkAutoRestart == null)
                return false;

            return chkAutoRestart.Checked;
        }

        private void BtnResetSettings_Click(object sender, EventArgs e)
        {
            if (applicationLogic == null)
                return;

            applicationLogic.ResetSettings();
            pnlDevices.Controls.Clear();
        }

        public void DoDragDrop(object sender, DragEventArgs e)
        {
            if (e == null || pnlDevices == null)
                return;

            if (e.Data!.GetFormats().Length >= 1 &&
                e.Data.GetData(format: e.Data.GetFormats()[0]) is DeviceControl &&
                sender is DeviceControl deviceControl)
            {
                var draggingControl = (DeviceControl)e.Data.GetData(e.Data.GetFormats()[0])!;
                var droppingOnControl = deviceControl;
                var indexDrop = pnlDevices.Controls.GetChildIndex(droppingOnControl);

                pnlDevices.Controls.SetChildIndex(draggingControl, indexDrop);
            }
        }

        private void ChkShowLagControl_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowLagControl == null)
                return;

            ShowLagControl(chkShowLagControl.Checked);
        }

        public bool? GetShowLagControl()
        {
            if (chkShowLagControl == null)
                return false;

            return chkShowLagControl.Checked;
        }

        public int? GetLagValue()
        {
            if (trbLag == null)
                return 1000;

            return trbLag.Value;
        }

        public void SetStreamFormat(SupportedStreamFormat format)
        {
            if (cmbStreamFormat == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<SupportedStreamFormat>(SetStreamFormat), new object[] { format });
                return;
            }
            if (IsDisposed) return;

            FillStreamFormats();
            for (int i = 0; i < cmbStreamFormat.Items.Count; i++)
            {
                if ((SupportedStreamFormat)((ComboboxItem)cmbStreamFormat.Items[i]!).Value == format)
                    cmbStreamFormat.SelectedIndex = i;
            }
            SetStreamFormat();
        }

        public void GetStreamFormat()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(GetStreamFormat));
                return;
            }
            if (IsDisposed) return;

            SetStreamFormat();
        }

        public SupportedStreamFormat GetSelectedStreamFormat()
        {
            if (InvokeRequired)
            {
                Invoke(new Func<SupportedStreamFormat>(GetSelectedStreamFormat));
                return SupportedStreamFormat.Mp3_320;
            }
            if (IsDisposed) return SupportedStreamFormat.Mp3_320;

            try
            {
                return (SupportedStreamFormat)((ComboboxItem)cmbStreamFormat.SelectedItem!).Value;
            }
            catch (Exception)
            {
                return SupportedStreamFormat.Mp3_320;
            }
        }

        private void CmbStreamFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetStreamFormat();
        }

        private void SetStreamFormat()
        {
            if (cmbStreamFormat == null || applicationLogic == null)
                return;

            if (cmbStreamFormat.SelectedItem != null)
            {
                var format = (SupportedStreamFormat)((ComboboxItem)cmbStreamFormat.SelectedItem).Value;
                applicationLogic.SetStreamFormat(format);
                captureEngine.Apply(BuildCaptureSettings());
                Classes.Theme.CurrentFormatLabel = Classes.Theme.FormatPillText(format);
                RefreshDeviceCards();
            }
        }

        // Format-pill text is app-global (one HTTP stream serves all devices) but each card only re-paints on
        // its own state/volume events - force a repaint so a format change is reflected immediately, even on
        // an already-playing card.
        private void RefreshDeviceCards()
        {
            if (pnlDevices == null) return;
            foreach (Control c in pnlDevices.Controls)
                if (c is UserControls.DeviceControl dc) dc.Invalidate();
        }

        private void CmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbLanguage == null)
                return;

            if (cmbLanguage.SelectedItem is LanguageItem item
                && !string.Equals(item.Code, Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                SetCulture(item.Code);
        }

        public void SetCulture(string culture)
        {
            if (applicationLogic == null)
                return;

            CultureInfo ci = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            // The artwork is rendered on whichever thread the streaming server answers on, and that thread
            // never saw the UI thread's culture - which is why the TV kept its old language. These two make
            // the choice the process-wide default, so every thread born after it agrees.
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            Classes.ArtworkRenderer.Invalidate();   // the TV screen is drawn in this language too
            ApplyLocalization();
            applicationLogic.SetCulture(culture);
        }

        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            if (txtLog == null || log == null)
                return;

            txtLog.Clear();
            log.Clear();
        }

        public void SetLogDeviceCommunication(bool logDeviceCommunication)
        {
            if (chkLogDeviceCommunication == null || tabControl == null)
                return;

            chkLogDeviceCommunication.Checked = logDeviceCommunication;
            tabStrip?.SetTabShown(tabPageLog, logDeviceCommunication);
            ApplyTheme(null, GetDarkMode());
        }

        public bool GetLogDeviceCommunication()
        {
            if (chkLogDeviceCommunication == null)
                return false;

            return chkLogDeviceCommunication.Checked;
        }

        public void SetStartApplicationWhenWindowsStarts(bool value)
        {
            chkStartApplicationWhenWindowsStarts.Checked = value;
        }

        public bool GetStartApplicationWhenWindowsStarts()
        {
            return chkStartApplicationWhenWindowsStarts.Checked;
        }

        private void ChkLogDeviceCommunication_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLogDeviceCommunication == null)
                return;

            SetLogDeviceCommunication(chkLogDeviceCommunication.Checked);
        }

        private void CheckForNewVersion(string currentVersion)
        {
            try
            {
                applicationLogic.StartTask(async () => {
                    try
                    {
                        var url = "https://api.github.com/repos/finflowly/KlangHub/releases/latest";
                        using (var handler = new HttpClientHandler())
                        {
                            handler.UseDefaultCredentials = true;
                            handler.UseProxy = false;

                            using (var client = new HttpClient(handler))
                            {
                                client.DefaultRequestHeaders.Add("KeepAlive", "false");
                                client.DefaultRequestHeaders.Add("User-Agent", "finflowly/KlangHub");

                                var response = await client.GetAsync(new Uri(url));
                                response.EnsureSuccessStatusCode();

                                var responseBody = response.Content.ReadAsStringAsync();

                                var doc = JsonDocument.Parse(responseBody.Result);
                                var latestRelease = doc.RootElement.GetProperty("tag_name").GetString()!.Replace("v", "");
                                if (latestRelease.CompareTo(currentVersion) > 0)
                                {
                                    var latestReleaseUrl = doc.RootElement.GetProperty("html_url").GetString()!;
                                    ShowLatestRelease(latestRelease, latestReleaseUrl);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"CheckForNewVersion: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Log($"CheckForNewVersion: {ex.Message}");
            }
        }

        private void ShowLatestRelease(string latestRelease, string latestReleaseUrl)
        {
            if (lblNewReleaseAvailable == null)
                return;

            if (IsDisposed) return;
            if (InvokeRequired)
            {
                Invoke(new Action<string, string>(ShowLatestRelease), new object[] { latestRelease, latestReleaseUrl });
                return;
            }

            lblNewReleaseAvailable.Text = $"{Properties.Strings.Label_NewVersionAvailable} ({latestRelease})";
            var link = new LinkLabel.Link
            {
                LinkData = latestReleaseUrl
            };
            lblNewReleaseAvailable.Links.Add(link);
            lblNewReleaseAvailable.Visible = true;
        }

        private void LblNewReleaseAvailable_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e == null)
                return;

            OpenUrl((e.Link!.LinkData as string)!);
        }

        private void ChkStartApplicationWhenWindowsStarts_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                WindowsStartup.StartApplicationWhenWindowsStarts(
                    chkStartApplicationWhenWindowsStarts.Checked, "KlangHub", System.Windows.Forms.Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void FillFilterDevices()
        {
            if (cmbFilterDevices == null)
                return;

            if (cmbFilterDevices.Items.Count == 0)
            {
                cmbFilterDevices.Items.Add(new ComboboxItem(FilterDevicesEnum.ShowAll));
                cmbFilterDevices.Items.Add(new ComboboxItem(FilterDevicesEnum.DevicesOnly));
                cmbFilterDevices.Items.Add(new ComboboxItem(FilterDevicesEnum.GroupsOnly));
                cmbFilterDevices.SelectedIndex = 0;
            }
        }

        public void SetFilterDevices(FilterDevicesEnum value)
        {
            if (cmbFilterDevices == null)
                return;

            FillFilterDevices();
            for (int i = 0; i < cmbFilterDevices.Items.Count; i++)
            {
                if ((FilterDevicesEnum)((ComboboxItem)cmbFilterDevices.Items[i]!).Value == value)
                    cmbFilterDevices.SelectedIndex = i;
            }
            ApplyFilter(value);
        }

        public FilterDevicesEnum? GetFilterDevices()
        {
            if (cmbFilterDevices == null)
                return null;

            return (FilterDevicesEnum)((ComboboxItem)cmbFilterDevices.SelectedItem!).Value;
        }

        private void CmbFilterDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbFilterDevices?.SelectedItem == null)
                return;

            ApplyFilter((FilterDevicesEnum)((ComboboxItem)cmbFilterDevices.SelectedItem).Value);
        }

        // 2.2b-4.7: MainForm owns filter visibility (keyed on each control's own IsGroup), replacing the
        // old Devices.SetFilterDevices -> device.GetDeviceControl().Visible reverse-coupling path.
        private void ApplyFilter(FilterDevicesEnum value)
        {
            if (pnlDevices == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<FilterDevicesEnum>(ApplyFilter), new object[] { value });
                return;
            }

            foreach (var dc in pnlDevices.Controls.OfType<DeviceControl>())
                dc.Visible = FilterDevices.ShowFilterDevices(dc.IsGroup, value);
        }

        public void SetStartLastUsedDevices(bool value)
        {
            if (chkAutoStartLastUsed == null)
                return;

            chkAutoStartLastUsed.Checked = value;
        }

        public bool? GetStartLastUsedDevices()
        {
            if (chkAutoStartLastUsed == null)
                return false;

            return chkAutoStartLastUsed.Checked;
        }

        public void SetSize(Size size)
        {
            logger.Log($"Set size, height: {size.Height} width: {size.Width}");
            Height = size.Height;
            Width = size.Width;
        }

        public Size GetSize()
        {
            return windowSize;
        }

        public void SetPosition(int? left, int? top)
        {
            if (left.HasValue && top.HasValue)
            {
                Left = left.Value;
                Top = top.Value;
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
        }

        public int GetLeft()
        {
            if (WindowState == FormWindowState.Normal)
                return Left;
            else
                return RestoreBounds.Left;
        }

        public int GetTop()
        {
            if (WindowState == FormWindowState.Normal)
                return Top;
            else
                return RestoreBounds.Top;
        }

        private void LinkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e == null)
                return;

            OpenUrl("https://github.com/finflowly/KlangHub/wiki#options");
        }

        private void OpenUrl(string url)
        {
            ProcessStartInfo psInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            try
            {
                Process.Start(psInfo);
            }
            catch (Exception)
            {
                MessageBox.Show($"There was an error opening the url {psInfo.FileName}");
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (Size.Width >= 50 && Size.Height >= 50)
                windowSize = Size;
        }

        public void ShowWavMeterValue(byte[] data)
        {
            try
            {
                var maximum = 0f;
                for (int index = 0; index < data.Length; index += 2)
                {
                    var sample = (short)((data[index + 1] << 8) | data[index + 0]);
                    var sample32 = sample / 32768f;
                    if (sample32 > maximum)
                        maximum = sample32;
                }
                volumeMeter.Amplitude = maximum;
            }
            catch (Exception ex)
            {
                logger.Log(ex, "MainFrom.ViewWav");
            }
        }

        /// <summary>
        /// Set the device buffer value.
        /// </summary>
        /// <param name="extraBufferInSecondsIn">buffer in seconds</param>
        public void SetExtraBufferInSeconds(int extraBufferInSecondsIn)
        {
            if (devices == null)
                return;

            for (int i = 0; i < cmbBufferInSeconds.Items.Count; i++)
            {
                if (int.Parse((string)cmbBufferInSeconds.Items[i]!) == extraBufferInSecondsIn)
                    cmbBufferInSeconds.SelectedIndex = i;
            }
        }

        /// <summary>
        /// Get the device buffer value (in seconds).
        /// </summary>
        /// <returns>buffer in seconds</returns>
        public int? GetExtraBufferInSeconds()
        {
            if (devices == null)
                return 0;

            return int.Parse((string)cmbBufferInSeconds.SelectedItem!);
        }

        /// <summary>
        /// Change the buffer on the devices.
        /// </summary>
        private void CmbBufferInSeconds_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbBufferInSeconds == null || devices == null)
                return;

            var bufferInSeconds = int.Parse((string)cmbBufferInSeconds.SelectedItem!);
            devices.SetExtraBufferInSeconds(bufferInSeconds);
        }

        public void SetRecordingDeviceID(string? recordingDeviceIDIn)
        {
            previousRecordingDeviceID = recordingDeviceIDIn;
            isRecordingDeviceSelected = false;
        }

        public string GetRecordingDeviceID()
        {
            try
            {
                if (cmbRecordingDevice.Items.Count == 0 || cmbRecordingDevice.SelectedItem == null)
                    return null!;

                return ((AudioCaptureDevice)cmbRecordingDevice.SelectedItem).Id;
            }
            catch (Exception)
            {
            }

            return null!;
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
            captureEngine?.Dispose();
            wavGenerator?.Dispose();
        }

        private void ChkAutoMute_CheckedChanged(object sender, EventArgs e)
        {
            SetAutoMute(chkAutoMute.Checked);
        }

        public void SetAutoMute(bool autoMute)
        {
            if (chkLogDeviceCommunication == null || tabControl == null)
                return;

            chkAutoMute.Checked = autoMute;
        }

        public bool GetAutoMute()
        {
            if (chkAutoMute == null)
                return false;

            return chkAutoMute.Checked;
        }

        public void RestartRecording()
        {
            captureEngine.Apply(BuildCaptureSettings());
        }

        public void SetMinimizeToTray(bool minimizeToTray)
        {
            if (chkMinimizeToTray == null)
                return;

            chkMinimizeToTray.Checked = minimizeToTray;
        }

        public bool GetMinimizeToTray()
        {
            if (chkMinimizeToTray == null)
                return false;

            return chkMinimizeToTray.Checked;
        }

        public void SetConvertMultiChannelToStereo(bool convertMultiChannelToStereo)
        {
            if (chkConvertMultiChannelToStereo == null)
                return;

            chkConvertMultiChannelToStereo.Checked = convertMultiChannelToStereo;
        }

        public bool GetConvertMultiChannelToStereo()
        {
            if (chkConvertMultiChannelToStereo == null)
                return false;

            return chkConvertMultiChannelToStereo.Checked;
        }

        public void SetDarkMode(bool darkmode)
        {
            // Warm-dark is KlangHub's fixed identity (2026-07-05). The light path is retired, so a persisted
            // or requested "false" is ignored — the app always renders the dark HiFi console.
            if (chkDarkMode != null)
                chkDarkMode.Checked = true;
            ApplyTheme(null, true);
        }

        public bool GetDarkMode()
        {
            return true; // fixed identity: always the warm-dark console (see SetDarkMode)
        }

        public void SetIP4AddressUsed(string ip4Address)
        {
            if (cmbIP4AddressUsed == null || string.IsNullOrEmpty(ip4Address))
                return;

            for (int i = 0; i < cmbIP4AddressUsed.Items.Count; i++)
            {
                var ipAddress = cmbIP4AddressUsed.Items[i]!.ToString();
                if (ip4Address == ipAddress)
                {
                    if (cmbIP4AddressUsed.SelectedIndex != i)
                    {
                        cmbIP4AddressUsed.SelectedIndex = i;
                    }
                }
            }
        }

        public string GetIP4AddressUsed()
        {
            if (cmbIP4AddressUsed.Items.Count == 0 || cmbIP4AddressUsed.SelectedItem == null)
                return string.Empty;

            return cmbIP4AddressUsed.SelectedItem.ToString()!;
        }

        public void ApplyTheme(Control? item, bool darkmode)
        {
            if (item == null)
            {
                item = this;
                BackColor = darkmode ? Classes.Theme.Ink : SystemColors.Control;
                ForeColor = darkmode ? Classes.Theme.Ivory : Color.Black;
                if (IsHandleCreated) Classes.DwmChrome.Apply(this, darkmode);
                else HandleCreated += (s, e) => Classes.DwmChrome.Apply(this, darkmode);
            }

            foreach (var control in item.Controls)
            {
                DoApplyTheme(control, darkmode);
                ApplyTheme((Control)control, darkmode);
            }
        }

        // The KlangHub "hi-fi console" theme: a warm-dark palette (Classes.Theme) applied across the stock
        // WinForms controls. Device cards own-draw themselves and are skipped here.
        private void DoApplyTheme(object item, bool darkmode)
        {
            var ink = darkmode ? Classes.Theme.Ink : Color.White;
            var surface = darkmode ? Classes.Theme.Surface : SystemColors.Control;
            var recessed = darkmode ? Classes.Theme.Ink2 : SystemColors.Window;
            var text = darkmode ? Classes.Theme.Ivory : Color.Black;
            var subtle = darkmode ? Classes.Theme.Slate : SystemColors.GrayText;

            switch (item)
            {
                case DeviceControl:
                    return; // owner-drawn; leave it alone
                case UserControls.CardPanel:
                case UserControls.HairLine:
                case UserControls.FieldFrame:
                case UserControls.PillButton:
                case UserControls.ConsoleTabStrip:
                    return; // owner-drawn against the token set; the generic cascade would flatten them
                case GroupBox gb:
                    gb.BackColor = ink; gb.ForeColor = subtle;
                    break;
                case UserControls.DarkTextBox dtb:
                    dtb.BackColor = Classes.Theme.Ink2; dtb.ForeColor = text;
                    break;
                case TextBox tb:
                    tb.BackColor = recessed; tb.ForeColor = text;
                    tb.BorderStyle = darkmode ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;
                case LinkLabel ll:
                    ll.BackColor = ink; ll.LinkColor = Classes.Theme.Amber;
                    ll.ActiveLinkColor = Classes.Theme.Ivory; ll.VisitedLinkColor = Classes.Theme.AmberDim;
                    break;
                case CheckBox chk:
                    chk.BackColor = Color.Transparent; chk.ForeColor = text; chk.FlatStyle = FlatStyle.Flat;
                    chk.FlatAppearance.BorderColor = Classes.Theme.Line;
                    break;
                case UserControls.DarkComboBox dcb:
                    dcb.BackColor = Classes.Theme.Ink2; dcb.ForeColor = text;
                    break;
                case ComboBox cb:
                    cb.BackColor = surface; cb.ForeColor = text; cb.FlatStyle = FlatStyle.Flat;
                    break;
                case FlowLayoutPanel flp when flp.Parent is not DeviceControl:
                    flp.BackColor = ink; flp.ForeColor = text;
                    break;
                case Panel pnl when pnl.Tag as string == "backdrop":
                case FlowLayoutPanel flp2 when flp2.Tag as string == "backdrop":
                    return;   // shows the shared backdrop through - a solid ink fill would block it
                case Panel pnl2 when pnl2.Parent is not DeviceControl && pnl2.Parent is not UserControls.CardPanel:
                    pnl2.BackColor = ink; pnl2.ForeColor = text;
                    break;
                case Button btn when btn.Parent is not DeviceControl:
                    btn.BackColor = surface; btn.ForeColor = text;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Classes.Theme.Line;
                    btn.FlatAppearance.MouseOverBackColor = darkmode ? Classes.Theme.Raised : SystemColors.ControlLight;
                    btn.UseVisualStyleBackColor = false;
                    break;
                case Label lbl when lbl.Parent is not DeviceControl && lbl.Name != "lblSectionSound" && lbl.Name != "lblSectionBehavior":
                    lbl.BackColor = Color.Transparent;
                    if (lbl.Tag as string != "keep-fg") lbl.ForeColor = subtle;
                    break;
            }
        }

        private bool premiumChromeApplied;

        /// <summary>
        /// Applies the premium "hi-fi console" chrome: the branded header band above the master-volume bar, and
        /// owner-drawn group boxes (the stock etched border is over-drawn so the dark theme reads cleanly).
        /// </summary>
        private void SetupPremiumChrome()
        {
            if (tabPageMain == null || premiumChromeApplied)
                return;
            premiumChromeApplied = true;

            Resize += (s, e) => RepaintBackdrop();
            ResizeEnd += (s, e) => RepaintBackdrop();
            grpVolume.Height = 64;   // one calm toolbar strip, not a 84-px group box
            ThemeGroupBoxBorder(grpVolume);
            ThemeGroupBoxBorder(grpDevices);
            ThemeGroupBoxBorder(grpLag);
            ThemeGroupBoxBorder(grpOptions);
            pnlDevices.BackColor = Classes.Theme.Ink;
            Classes.DwmChrome.UseDarkScrollbars(pnlDevices);
            pnlDevices.Resize += (s, e) => ResizeRoomBars();
            pnlDevices.ControlAdded += (s, e) => { if (groupByRoom && e.Control is DeviceControl) BeginInvoke(new Action(RebuildDeviceLayout)); };
            pnlDevices.ControlRemoved += (s, e) => { if (groupByRoom && e.Control is DeviceControl) BeginInvoke(new Action(RebuildDeviceLayout)); };
            EnableBackdrop(tabPageMain);
            EnableBackdrop(tabPageOptions);
            EnableBackdrop(tabPageLog);
            EnableBackdrop(pnlDevices);
            pnlVolumeAllButtons.BackColor = Color.Transparent;   // WinForms paints the parent through it
            pnlVolumeAllButtons.Tag = "backdrop";
            SetupComboBoxDarkDraw(this);

            var icon = Classes.Theme.LoadAppIcon();
            if (icon != null) Icon = icon;   // amber-ring brand mark on the title bar + taskbar

            if (volumeMeter != null) { volumeMeter.BackColor = Classes.Theme.Ink2; volumeMeter.ForeColor = Classes.Theme.Amber; }
            // The concept has no global VU meter — the live level lives on each card (the card's signature).
            // Retire the header meter + its "dB" label so the top row reads as one calm strip.
            if (pnlVolumeMeter != null) pnlVolumeMeter.Visible = false;

            SetupMasterVolumeFader();
            SetupRoomSummary();
            SetupTabStrip();
            SetupSettingsPage();
        }

        private System.Windows.Forms.Timer? roomSummaryTimer;
        private Label? lblRoomSummaryTitle, lblRoomSummarySubtitle;

        // The concept places a live "N Geräte · M spielen" / "format · verlustfrei ans ganze Haus" summary
        // to the LEFT of the master-volume fader, replacing the generic "ALLE RÄUME" group caption. Two
        // stacked labels (different weights match the concept's title/subtitle) inserted before the fader.
        private void SetupRoomSummary()
        {
            if (grpVolume == null || grpVolume.Controls.ContainsKey("pnlRoomSummary"))
                return;   // the stack lives in grpVolume since the toolbar rebuild - guard the real parent

            grpVolume.Text = string.Empty; // no more generic uppercase caption on this row

            lblRoomSummaryTitle = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Height = 22,
                Font = new Font(Classes.Theme.Name.FontFamily, 11.5f, Classes.Theme.Name.Style),
                ForeColor = Classes.Theme.Ivory,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Tag = "keep-fg",   // the generic label cascade would flatten the title into slate
            };
            lblRoomSummarySubtitle = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Height = 18,
                Font = Classes.Theme.Small,
                ForeColor = Classes.Theme.Slate,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Tag = "keep-fg",
            };
            var stack = new FlowLayoutPanel
            {
                Name = "pnlRoomSummary",
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(6, 6, 24, 4),
                BackColor = Color.Transparent,
            };
            stack.Controls.Add(lblRoomSummaryTitle);
            stack.Controls.Add(lblRoomSummarySubtitle);
            stack.Margin = new Padding(2, 8, 24, 4);
            stack.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            grpVolume.Controls.Add(stack);
            stack.Location = new Point(4, 10);
            stack.BringToFront();

            RefreshRoomSummary();
            roomSummaryTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            roomSummaryTimer.Tick += (s, e) => RefreshRoomSummary();
            roomSummaryTimer.Start();
        }

        // The concept board's header line, now fully localized and built from what discovery actually knows:
        // a title that names the scope and how much of it is playing, and a quiet second line with the count,
        // how many of those are multi-room groups, the stream format and what that format means for quality.
        private void RefreshRoomSummary()
        {
            if (lblRoomSummaryTitle == null || lblRoomSummarySubtitle == null) return;
            var (total, playing, groups) = CountDevices();

            string title = Properties.Strings.Label_RoomSummaryTitle_Text;
            if (playing > 0)
            {
                string p = playing == 1
                    ? Properties.Strings.Label_RoomSummaryPlayingOne_Text
                    : string.Format(Properties.Strings.Label_RoomSummaryPlayingMany_Text, playing);
                title += " · " + p;
            }
            lblRoomSummaryTitle.Text = title;

            if (total == 0)
            {
                lblRoomSummarySubtitle.Text = Properties.Strings.Label_RoomSummaryEmpty_Text;
                return;
            }

            string rooms = total == 1
                ? Properties.Strings.Label_RoomSummaryDevicesOne_Text
                : string.Format(Properties.Strings.Label_RoomSummaryDevicesMany_Text, total);
            if (groups > 0)
            {
                string g = groups == 1
                    ? Properties.Strings.Label_RoomSummaryGroupsOne_Text
                    : string.Format(Properties.Strings.Label_RoomSummaryGroupsMany_Text, groups);
                rooms += " · " + g;
            }
            string quality = IsLosslessFormat()
                ? Properties.Strings.Label_RoomSummaryLossless_Text
                : Properties.Strings.Label_RoomSummaryCompressed_Text;
            lblRoomSummarySubtitle.Text = $"{rooms} · {Classes.Theme.CurrentFormatLabel} · {quality}";
        }

        /// <summary>True while the selected stream format carries the original samples (WAV / FLAC).</summary>
        private static bool IsLosslessFormat() =>
            !Classes.Theme.CurrentFormatLabel.StartsWith("MP3", StringComparison.OrdinalIgnoreCase);


        // ================= grouping the grid by room =================================================
        // Once speakers carry a room, the flat grid stops being the honest picture of a home: what the eye
        // wants is the kitchen next to the kitchen. The FlowLayoutPanel keeps its cards - a full-width room
        // bar is inserted before each block and a flow break after it, so the layout still reflows on resize
        // and nothing about the cards themselves has to change.
        private readonly List<UserControls.RoomBarControl> roomBars = new();
        private bool groupByRoom;

        /// <summary>Whether the device grid is grouped into rooms (persisted with the other settings).</summary>
        public bool GetGroupByRoom() => groupByRoom;

        public void SetGroupByRoom(bool value)
        {
            if (groupByRoom == value) return;
            groupByRoom = value;
            if (btnGroupRooms != null) btnGroupRooms.Primary = value;
            RebuildDeviceLayout();   // persisted with the rest of the settings on close (ApplicationLogic)
        }

        /// <summary>Speakers of one room, in the order they appear in the grid.</summary>
        private IReadOnlyList<DeviceControl> DevicesInRoom(string room) =>
            pnlDevices.Controls.OfType<DeviceControl>()
                .Where(d => d.Visible && string.Equals(RoomOf(d), room, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        /// <summary>A card's room, or the "no room yet" bucket - so every speaker belongs somewhere.</summary>
        private static string RoomOf(DeviceControl d) =>
            string.IsNullOrWhiteSpace(d.Room) ? Properties.Strings.Room_Unassigned_Text : d.Room!.Trim();

        /// <summary>
        /// Re-orders the grid: either the plain card sequence, or room bar + that room's cards, room by room.
        /// Called whenever something changes that can move a card between rooms.
        /// </summary>
        private void RebuildDeviceLayout()
        {
            if (pnlDevices == null || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(RebuildDeviceLayout)); return; }

            pnlDevices.SuspendLayout();
            try
            {
                foreach (var bar in roomBars)
                {
                    pnlDevices.Controls.Remove(bar);
                    bar.Dispose();
                }
                roomBars.Clear();

                var cards = pnlDevices.Controls.OfType<DeviceControl>().ToList();
                foreach (var c in cards) pnlDevices.SetFlowBreak(c, false);

                if (!groupByRoom)
                    return;

                // rooms in alphabetical order, with the unassigned bucket last - it is a to-do list, not a room
                string unassigned = Properties.Strings.Room_Unassigned_Text;
                var rooms = cards.Where(c => c.Visible).Select(RoomOf).Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(r => string.Equals(r, unassigned, StringComparison.CurrentCultureIgnoreCase) ? 1 : 0)
                    .ThenBy(r => r, StringComparer.CurrentCulture)
                    .ToList();

                int index = 0;
                foreach (var room in rooms)
                {
                    string captured = room;
                    var bar = new UserControls.RoomBarControl(captured, () => DevicesInRoom(captured));
                    bar.RoomChanged += (s, e) => RefreshRoomSummary();
                    roomBars.Add(bar);

                    pnlDevices.Controls.Add(bar);
                    pnlDevices.Controls.SetChildIndex(bar, index++);
                    pnlDevices.SetFlowBreak(bar, true);
                    SizeRoomBar(bar);

                    var inRoom = cards.Where(c => c.Visible && string.Equals(RoomOf(c), captured, StringComparison.CurrentCultureIgnoreCase)).ToList();
                    foreach (var card in inRoom)
                        pnlDevices.Controls.SetChildIndex(card, index++);
                    if (inRoom.Count > 0)
                        pnlDevices.SetFlowBreak(inRoom[^1], true);
                }
            }
            finally
            {
                pnlDevices.ResumeLayout(true);
                pnlDevices.Invalidate(true);
            }
        }

        /// <summary>A room bar spans the grid, so it has to follow the panel's width.</summary>
        private void SizeRoomBar(UserControls.RoomBarControl bar)
        {
            int available = pnlDevices.ClientSize.Width - bar.Margin.Horizontal - 4;
            bar.Width = Math.Max(280, available);
        }

        private void ResizeRoomBars()
        {
            foreach (var bar in roomBars) SizeRoomBar(bar);
        }

        // ================= Einstellungen: the settings page, rebuilt as console cards =================
        // The designer's flat two-column flow (label column | control column) read as a form dialog from 2010:
        // stock combo chrome, a white text box, hairline dividers and no shared material with the Raeume page.
        // This rebuild re-hosts the very same controls (every binding, event and Get/Set stays untouched) inside
        // the concept's surface cards, on one 8-px grid, with the field wells and pill buttons the rest of the
        // app already uses.
        private UserControls.CardPanel? cardSound, cardBehavior;
        private Panel? settingsScroll;

        private void SetupSettingsPage()
        {
            if (tabPageOptions == null || settingsScroll != null)
                return;

            // The old containers keep existing (the designer still wires them) but leave the visual tree.
            grpOptions.Visible = false;
            tabPageOptions.Controls.Remove(grpOptions);

            settingsScroll = new Panel
            {
                Name = "pnlSettingsScroll",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Classes.Theme.Ink,
            };
            tabPageOptions.Controls.Add(settingsScroll);
            Classes.DwmChrome.UseDarkScrollbars(settingsScroll);
            EnableBackdrop(settingsScroll);

            cardSound = BuildSoundCard();
            cardBehavior = BuildBehaviorCard();
            var footer = BuildSettingsFooter();

            // Dock=Top siblings stack in REVERSE collection order, so add bottom-up.
            settingsScroll.Controls.Add(footer);
            settingsScroll.Controls.Add(Spacer(16));
            settingsScroll.Controls.Add(cardBehavior);
            settingsScroll.Controls.Add(Spacer(16));
            settingsScroll.Controls.Add(cardSound);
            settingsScroll.Controls.Add(Spacer(4));

            // A settings column wider than ~900 px pushes labels and switches apart until the rows stop reading
            // as pairs; cap it with right padding rather than absolute widths so it still reflows on any DPI.
            settingsScroll.Resize += (s, e) => ClampSettingsWidth();
            ClampSettingsWidth();
        }

        private void ClampSettingsWidth()
        {
            if (settingsScroll == null) return;
            // ClientSize already includes the padding band (only DisplayRectangle subtracts it), so the
            // slack must be measured against ClientSize alone - adding Padding.Right back in made every
            // resize event stack another band on top and squeezed the column shut.
            int slack = Math.Max(0, settingsScroll.ClientSize.Width - 920);
            var want = new Padding(0, 0, slack, 8);
            if (settingsScroll.Padding != want) settingsScroll.Padding = want;
        }

        private static Panel Spacer(int h) =>
            new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent, Tag = "backdrop" };

        /// <summary>Card 1 - the sound profile and connection fields, on a two-column label/field grid.</summary>
        private UserControls.CardPanel BuildSoundCard()
        {
            var card = new UserControls.CardPanel
            {
                Name = "cardSound",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(22, 46, 22, 20),   // top inset leaves room for the tracked caption
                Caption = Properties.Strings.Label_Section_Sound_Text,
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));   // the field follows the card width

            AddField(grid, lblIpAddressUsed, WrapField(cmbIP4AddressUsed));
            AddField(grid, lblDevice, WrapField(cmbRecordingDevice));
            AddField(grid, lblStreamFormat, WrapField(cmbStreamFormat));
            AddField(grid, lblLanguage, WrapField(cmbLanguage));
            AddField(grid, lblFilterDevices, WrapField(cmbFilterDevices));
            AddField(grid, lblBufferInSeconds, WrapField(cmbBufferInSeconds));
            AddField(grid, lblStreamTitle, WrapField(txtStreamTitle));

            card.Controls.Add(grid);
            return card;
        }

        /// <summary>
        /// Puts an input inside the drawn field well. A TextBox simply drops its border; a ComboBox cannot,
        /// so it is filled to the well and clipped to a rounded region (DarkComboBox.ClipToWell) that cuts
        /// its light native frame off. Either way the visible shape is the well: ink-2, 9-px, one hairline.
        /// </summary>
        private UserControls.FieldFrame WrapField(Control input)
        {
            var well = new UserControls.FieldFrame { Margin = new Padding(0, 5, 0, 5), Padding = new Padding(0) };
            input.Parent?.Controls.Remove(input);
            if (input is TextBox tb) { tb.BorderStyle = BorderStyle.None; tb.Multiline = false; }
            input.BackColor = Classes.Theme.Ink2;
            input.ForeColor = Classes.Theme.Ivory;
            input.Font = Classes.Theme.Body;
            input.Dock = DockStyle.None;
            well.Controls.Add(input);

            void Fit()
            {
                if (input is UserControls.DarkComboBox combo)
                {
                    // A drop-down ComboBox forces its own PreferredHeight, so filling the well is impossible;
                    // centre it and let it own the full width (its rounded clip becomes the visible field).
                    combo.Width = well.ClientSize.Width;
                    combo.Top = Math.Max(0, (well.ClientSize.Height - combo.Height) / 2);
                    combo.Left = 0;
                    combo.ClipToWell();
                }
                else
                {
                    input.SetBounds(12, Math.Max(0, (well.ClientSize.Height - input.Height) / 2),
                                    Math.Max(10, well.ClientSize.Width - 24), input.Height);
                }
            }
            well.Resize += (s, e) => Fit();
            well.HandleCreated += (s, e) => Fit();
            Fit();

            input.GotFocus += (s, e) => { well.Focused2 = true; well.Invalidate(); };
            input.LostFocus += (s, e) => { well.Focused2 = false; well.Invalidate(); };
            return well;
        }

        /// <summary>One label/field row on the settings grid - 48 px tall, vertically centred label.</summary>
        private void AddField(TableLayoutPanel grid, Label label, Control field)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            label.Parent?.Controls.Remove(label);
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.Margin = new Padding(0, 5, 16, 5);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = Classes.Theme.Body;
            label.ForeColor = Classes.Theme.Slate;
            label.BackColor = Color.Transparent;
            label.Tag = "keep-fg";

            field.Parent?.Controls.Remove(field);
            field.Dock = DockStyle.Fill;
            field.Margin = new Padding(0, 5, 0, 5);

            grid.Controls.Add(label, 0, row);
            grid.Controls.Add(field, 1, row);
        }

        /// <summary>Card 2 - the behaviour toggles, one 38-px switch row each, hairline-separated.</summary>
        private UserControls.CardPanel BuildBehaviorCard()
        {
            var card = new UserControls.CardPanel
            {
                Name = "cardBehavior",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(22, 46, 22, 14),
                Caption = Properties.Strings.Label_Section_Behavior_Text,
            };

            var stack = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
            };

            // Dock=Top stacking is reverse-of-collection, so add the list bottom-up to keep the reading order.
            var toggles = new UserControls.ToggleSwitch[]
            {
                chkHook, chkShowWindowOnStart, chkStartApplicationWhenWindowsStarts, chkAutoStart,
                chkAutoStartLastUsed, chkAutoRestart, chkLogDeviceCommunication, chkAutoMute,
                chkMinimizeToTray, chkConvertMultiChannelToStereo, chkShowLagControl, chkDarkMode,
            };
            bool seenVisible = false;
            for (int i = toggles.Length - 1; i >= 0; i--)
            {
                var t = toggles[i];
                t.Parent?.Controls.Remove(t);
                t.Dock = DockStyle.Top;
                t.AutoSize = false;
                t.Height = 38;
                t.Margin = new Padding(0);
                // Dock=Top reverses the order, so a line added AFTER a toggle ends up ABOVE it: only add one
                // once a visible row is already stacked, never before the first one.
                if (t.Visible && seenVisible) stack.Controls.Add(new UserControls.HairLine());
                stack.Controls.Add(t);
                if (t.Visible) seenVisible = true;
            }

            card.Controls.Add(stack);
            return card;
        }

        /// <summary>The quiet closing row: reset action left, help link, version and signature below.</summary>
        private Panel BuildSettingsFooter()
        {
            var footer = new Panel
            {
                Name = "pnlSettingsFooter",
                Dock = DockStyle.Top,
                Height = 112,
                BackColor = Color.Transparent,
                Tag = "backdrop",
            };

            btnResetSettings.Parent?.Controls.Remove(btnResetSettings);
            btnResetSettings.AutoSize = false;
            btnResetSettings.Size = new Size(214, 36);
            btnResetSettings.Location = new Point(0, 8);
            footer.Controls.Add(btnResetSettings);

            linkHelp.Parent?.Controls.Remove(linkHelp);
            linkHelp.AutoSize = true;
            linkHelp.Location = new Point(230, 17);
            linkHelp.Font = Classes.Theme.Body;
            linkHelp.LinkBehavior = LinkBehavior.HoverUnderline;
            linkHelp.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(linkHelp);

            lblNewReleaseAvailable.Parent?.Controls.Remove(lblNewReleaseAvailable);
            lblNewReleaseAvailable.AutoSize = true;
            lblNewReleaseAvailable.Dock = DockStyle.None;
            lblNewReleaseAvailable.Location = new Point(0, 58);
            lblNewReleaseAvailable.Padding = new Padding(0);
            lblNewReleaseAvailable.Font = Classes.Theme.Small;
            footer.Controls.Add(lblNewReleaseAvailable);

            lblVersion.Parent?.Controls.Remove(lblVersion);
            lblVersion.AutoSize = true;
            lblVersion.Dock = DockStyle.None;
            lblVersion.Location = new Point(0, 78);
            lblVersion.Padding = new Padding(0);
            lblVersion.Font = Classes.Theme.Small;
            lblVersion.ForeColor = Classes.Theme.Slate2;
            lblVersion.Tag = "keep-fg";
            footer.Controls.Add(lblVersion);

            // A small, discreet brand signature - not legally required (see docs/THIRD-PARTY-LICENSES.md and
            // README.md for the formal MIT attribution to the original author), just a quiet personal line.
            var credit = new Label
            {
                Name = "lblCredit",
                Text = Properties.Strings.Label_Credit_Text,
                UseMnemonic = false,   // otherwise "Neo & Trinity" loses its ampersand to an accelerator
                AutoSize = true,
                Location = new Point(0, 96),
                Font = new Font(Classes.Theme.Small.FontFamily, 8f, FontStyle.Italic),
                ForeColor = Classes.Theme.Slate2,
                BackColor = Color.Transparent,
                Tag = "keep-fg",
            };
            footer.Controls.Add(credit);
            return footer;
        }

        // The stock TabControl was replaced by an owner-drawn strip over plain panel pages (see
        // UserControls/ConsoleTabStrip): its native body frame is what drew the light hairline around the whole
        // window, and owner-draw could never reach it. The strip is created here and docked above the page host.
        private UserControls.ConsoleTabStrip? tabStrip;
        private UserControls.PillButton? btnGroupRooms;

        private void SetupTabStrip()
        {
            if (tabControl == null || tabStrip != null)
                return;

            tabControl.BackColor = Classes.Theme.Ink;
            tabStrip = new UserControls.ConsoleTabStrip();
            tabStrip.AddTab(tabPageMain, Properties.Strings.Tab_Main_Text);
            tabStrip.AddTab(tabPageOptions, Properties.Strings.Tab_Options_Text);
            tabStrip.AddTab(tabPageLog, Properties.Strings.Tab_Log_Text);
            Controls.Add(tabStrip);            // index 1 => docks Top first, the Fill page host keeps the rest
            tabStrip.SetTabShown(tabPageLog, chkLogDeviceCommunication?.Checked ?? false);

            if (txtLog != null) Classes.DwmChrome.UseDarkScrollbars(txtLog); // dark scrollbar instead of a light native one
        }

        /// <summary>Re-applies every caption this code owns after a language change (the designer-bound
        /// texts are handled by ApplyLocalization itself).</summary>
        /// <summary>Restores Ctrl+Tab / Ctrl+Shift+Tab page switching, which came with the stock TabControl
        /// and had to be re-implemented alongside the owner-drawn strip.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (tabStrip != null && (keyData & Keys.Control) == Keys.Control && (keyData & Keys.KeyCode) == Keys.Tab)
                if (tabStrip.StepSelection((keyData & Keys.Shift) == Keys.Shift ? -1 : 1))
                    return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void RefreshTabCaptions()
        {
            if (cardSound != null) { cardSound.Caption = Properties.Strings.Label_Section_Sound_Text; cardSound.Invalidate(); }
            if (cardBehavior != null) { cardBehavior.Caption = Properties.Strings.Label_Section_Behavior_Text; cardBehavior.Invalidate(); }
            RefreshRoomSummary();
            if (tabStrip == null) return;
            tabStrip.SetTabText(tabPageMain, Properties.Strings.Tab_Main_Text);
            tabStrip.SetTabText(tabPageOptions, Properties.Strings.Tab_Options_Text);
            tabStrip.SetTabText(tabPageLog, Properties.Strings.Tab_Log_Text);
        }

        // Replaces the old Lauter/Leiser/Alle-stumm buttons with a single master-volume fader (per the approved
        // UI concept): dragging it sets every visible card to that absolute level (each card still enforces its
        // own per-speaker maximum-volume cap); the mute icon replays the existing all-devices mute toggle.
        private void SetupMasterVolumeFader()
        {
            if (pnlVolumeAllButtons == null || pnlVolumeAllButtons.Controls.ContainsKey("masterVolume"))
                return;

            btnVolumeUp.Visible = false;
            btnVolumeDown.Visible = false;
            btnVolumeMute.Visible = false;

            // right-aligned toolbar cluster: [Erneut suchen] [master fader] reading right-to-left
            pnlVolumeAllButtons.Dock = DockStyle.Right;
            pnlVolumeAllButtons.FlowDirection = FlowDirection.RightToLeft;
            pnlVolumeAllButtons.WrapContents = false;
            btnScan.Margin = new Padding(10, 9, 4, 4);
            btnScan.AutoSize = false;
            btnScan.Size = new Size(168, 36);
            btnGroupRooms = new UserControls.PillButton
            {
                Name = "btnGroupRooms",
                Text = Properties.Strings.Button_GroupRooms_Text,
                AutoSize = false,
                Size = new Size(126, 36),
                Margin = new Padding(8, 9, 2, 4),
                Primary = groupByRoom,
            };
            btnGroupRooms.Click += (s, e) => SetGroupByRoom(!groupByRoom);
            pnlVolumeAllButtons.Controls.Add(btnGroupRooms);

            var master = new UserControls.MasterVolumeControl { Name = "masterVolume", Margin = new Padding(3, 9, 2, 4) };
            master.Size = new Size(152, 36);
            master.VolumeDragged += t => { foreach (Control c in pnlDevices.Controls) if (c is UserControls.DeviceControl dc) dc.ApplyVolumeAbsolute(t); };
            master.MuteClicked += () => devices?.VolumeMute();
            pnlVolumeAllButtons.Controls.Add(master);
            pnlVolumeAllButtons.Controls.SetChildIndex(btnScan, 0);  // right-to-left: first child sits rightmost
            pnlVolumeAllButtons.Controls.SetChildIndex(btnGroupRooms, 1);
            pnlVolumeAllButtons.Controls.SetChildIndex(master, 2);

            // The toolbar has to share one line with the live summary on the left. When the window gets too
            // narrow for all three, the room toggle steps back first - it is the one control with a home in
            // the settings too, and hiding it beats letting the fader collide with the text.
            void FitToolbar()
            {
                // The toggle only steps back when the window is genuinely too narrow for the row; at the
                // default size it has to be there, otherwise the feature is invisible.
                bool room = grpVolume.ClientSize.Width > 560;
                if (btnGroupRooms != null && btnGroupRooms.Visible != room) btnGroupRooms.Visible = room;

                // The summary shares the line with the toolbar and must give way to it: measured against what
                // the controls on the right actually take, and truncated with an ellipsis rather than sliding
                // underneath them.
                int taken = pnlVolumeAllButtons.PreferredSize.Width;
                int free = Math.Max(140, grpVolume.ClientSize.Width - taken - 30);
                if (lblRoomSummaryTitle != null) lblRoomSummaryTitle.Width = free;
                if (lblRoomSummarySubtitle != null) lblRoomSummarySubtitle.Width = free;
            }
            grpVolume.Resize += (s, e) => FitToolbar();
            FitToolbar();
        }

        // The app now wears the same picture the Chromecast puts on the TV (Resources/artwork.png, served
        // as /artwork.png to the receiver): one continuous, window-anchored backdrop behind everything, laid
        // under a heavy ink veil so cards, text and meters keep their contrast. Every surface that opts in
        // paints its own slice of that one image, so the panel seams stay invisible.
        private readonly List<Control> backdropSurfaces = new();

        private void EnableBackdrop(Control c)
        {
            if (c == null || backdropSurfaces.Contains(c)) return;
            SetDoubleBuffered(c);
            c.Paint += (s, e) => Classes.Theme.PaintBackdrop(e.Graphics, c);
            if (c is ScrollableControl sc && sc.AutoScroll)
                sc.Scroll += (s, e) => sc.Invalidate();   // a scrolled panel blits its old pixels; redraw them
            backdropSurfaces.Add(c);
            c.Invalidate();
        }

        /// <summary>The backdrop is scaled to the WINDOW, so every surface has to repaint whenever the window
        /// changes size - otherwise each panel keeps a slice of the old scale and the image tears at the seams.
        /// Stock containers do not invalidate on resize, so drive it from the form.</summary>
        private void RepaintBackdrop()
        {
            foreach (var c in backdropSurfaces)
                if (!c.IsDisposed) c.Invalidate();
        }

        /// <summary>Turns on double buffering for a stock container (the property is protected), so the
        /// backdrop does not flicker while the window resizes.</summary>
        private static void SetDoubleBuffered(Control c)
        {
            try
            {
                typeof(Control).GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(c, true, null);
            }
            catch { /* best effort - a non-buffered backdrop still draws correctly */ }
        }

        // WinForms DropDownList combos ignore BackColor, so owner-draw them for the dark theme.
        private void SetupComboBoxDarkDraw(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is UserControls.DarkComboBox)
                {
                    // paints itself (closed field and rows) - a second handler would draw over it
                }
                else if (c is ComboBox cb)
                {
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.DrawItem -= ComboBox_DrawItem;
                    cb.DrawItem += ComboBox_DrawItem;
                }
                if (c.HasChildren) SetupComboBoxDarkDraw(c);
            }
        }

        private void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox cb) return;
            bool dark = GetDarkMode();
            bool selected = (e.State & DrawItemState.Selected) != 0;

            // This handler paints BOTH the closed field (ComboBoxEdit) and the drop-down rows. They are two
            // different surfaces: the closed field is the recessed ink-2 well of the settings page, the rows
            // are a raised list. Painting the field in the list colour is what made every field read as a
            // light box inside its own well.
            bool isField = (e.State & DrawItemState.ComboBoxEdit) != 0;
            var bounds = isField ? cb.ClientRectangle : e.Bounds;
            var bg = !dark ? (selected && !isField ? SystemColors.Highlight : SystemColors.Window)
                   : isField ? Classes.Theme.Ink2
                   : selected ? Classes.Theme.Raised
                   : Classes.Theme.Surface;
            var fg = dark ? Classes.Theme.Ivory : (selected && !isField ? SystemColors.HighlightText : SystemColors.ControlText);

            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, bounds);
            if (selected && !isField && dark)
                using (var accent = new SolidBrush(Classes.Theme.Amber))
                    e.Graphics.FillRectangle(accent, bounds.X, bounds.Y, 3, bounds.Height);

            if (e.Index >= 0)
            {
                var r = new Rectangle(bounds.X + 11, bounds.Y, bounds.Width - (isField ? 40 : 20), bounds.Height);
                TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font, r, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix);
            }
        }

        private (int total, int playing, int groups) CountDevices()
        {
            int total = 0, playing = 0, groups = 0;
            if (pnlDevices != null)
                foreach (var c in pnlDevices.Controls)
                    if (c is UserControls.DeviceControl dc)
                    {
                        total++;
                        if (dc.IsPlaying) playing++;
                        if (dc.IsGroup) groups++;
                    }
            return (total, playing, groups);
        }

        // Over-draw the stock GroupBox etched border with Ink (children paint themselves on top) and re-draw the
        // caption as a quiet uppercase slate label - so the box reads as a clean section, not a 3D frame.
        private void ThemeGroupBoxBorder(GroupBox gb)
        {
            if (gb == null) return;
            SetDoubleBuffered(gb);
            gb.Paint += (s, e) =>
            {
                if (!GetDarkMode()) return;
                var g = e.Graphics;
                // the backdrop covers the whole box, etched 3D border included - no ink patches needed
                Classes.Theme.PaintBackdrop(g, gb);
                if (!string.IsNullOrEmpty(gb.Text))
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using var tb = new SolidBrush(Classes.Theme.Slate);
                    g.DrawString(gb.Text.ToUpperInvariant(), Classes.Theme.Label, tb, 6, 1);
                }
            };
            gb.Invalidate();
        }

        /// <summary>
        /// Sizes the window to the number of device tiles so the first start is already well-proportioned and it
        /// re-fits when a late speaker appears (some announce ~40 s in). Measures the current non-device chrome at
        /// runtime, so it needs no hard-coded layout constants, and clamps to the screen's working area.
        /// </summary>
        public void AutoSizeToDeviceCount()
        {
            if (InvokeRequired) { Invoke(new Action(AutoSizeToDeviceCount)); return; }
            if (pnlDevices == null || WindowState != FormWindowState.Normal) return;
            if (pnlDevices.Width <= 0 || pnlDevices.Height <= 0) return;

            int count = 0;
            foreach (var c in pnlDevices.Controls)
                if (c is UserControls.DeviceControl dc && dc.Visible) count++;
            if (count == 0) return;

            int cols = count <= 1 ? 1 : count > 6 ? 3 : 2;
            int rows = (count + cols - 1) / cols;
            const int cellW = 322 + 14, cellH = 172 + 14;   // card + margins
            int needPnlW = cols * cellW + 20;                // + scrollbar / inner padding
            int needPnlH = rows * cellH + 10;

            int chromeW = ClientSize.Width - pnlDevices.Width;   // everything that isn't the device viewport
            int chromeH = ClientSize.Height - pnlDevices.Height;

            var wa = Screen.FromControl(this).WorkingArea;
            int wantW = Math.Min(chromeW + needPnlW, wa.Width - 40);
            int wantH = Math.Min(chromeH + needPnlH, wa.Height - 40);
            wantW = Math.Max(wantW, 520);
            wantH = Math.Max(wantH, 420);

            if (Math.Abs(wantW - ClientSize.Width) < 8 && Math.Abs(wantH - ClientSize.Height) < 8)
                return;   // already about right - avoid thrash

            ClientSize = new Size(wantW, wantH);

            // keep the window on-screen after growing
            int nx = Math.Max(wa.Left, Math.Min(Left, wa.Right - Width));
            int ny = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height));
            if (nx != Left || ny != Top) Location = new Point(nx, ny);
        }

        private void chkDarkMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkDarkMode == null)
                return;

            ApplyTheme(null, chkDarkMode.Checked);
        }

        public void SetStreamTitle(string streamTitle)
        {
            if (txtStreamTitle == null)
                return;

            txtStreamTitle.Text = streamTitle;
        }

        public string GetStreamTitle()
        {
            if (txtStreamTitle == null)
                return string.Empty;

            return txtStreamTitle.Text;
        }
    }
}
