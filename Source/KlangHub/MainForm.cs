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
            // Displayed as three parts (Major.Minor.Build) - the compiler always pads AssemblyVersion's omitted
            // Revision to 0, which Version.ToString() would otherwise print as a trailing ".0" (e.g. "0.0.1.0").
            var av = assembly.GetName().Version;
            var appVersion = av != null ? $"{av.Major}.{av.Minor}.{av.Build}" : string.Empty;
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
            grpDevices.Text = Properties.Strings.Group_Devices_Text;
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
            lblBufferInSeconds.Text = Properties.Strings.Label_BufferInSeconds_Text;
            lblStreamTitle.Text = Properties.Strings.Label_StreamTitle_Text;
            chkDarkMode.Text = Properties.Strings.Check_DarkMode_Text;

            if (cmbLanguage.Items.Count == 0)
            {
                cmbLanguage.Items.Add(Resource.Get("Language", CultureInfo.GetCultureInfo("en")));
                cmbLanguage.Items.Add(Resource.Get("Language", CultureInfo.GetCultureInfo("fr")));
                cmbLanguage.Items.Add(Resource.Get("Language", CultureInfo.GetCultureInfo("de")));
            }
            else
            {
                if (cmbLanguage.Items[0]!.ToString() != Resource.Get("Language", CultureInfo.GetCultureInfo("en")))
                    cmbLanguage.Items[0] = Resource.Get("Language", CultureInfo.GetCultureInfo("en"));
                if (cmbLanguage.Items[1]!.ToString() != Resource.Get("Language", CultureInfo.GetCultureInfo("fr")))
                    cmbLanguage.Items[1] = Resource.Get("Language", CultureInfo.GetCultureInfo("fr"));
                if (cmbLanguage.Items.Count > 2 && cmbLanguage.Items[2]!.ToString() != Resource.Get("Language", CultureInfo.GetCultureInfo("de")))
                    cmbLanguage.Items[2] = Resource.Get("Language", CultureInfo.GetCultureInfo("de"));
            }
            var lang = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            var langIndex = lang == "fr" ? 1 : lang == "de" ? 2 : 0;
            if (cmbLanguage.SelectedIndex != langIndex)
                cmbLanguage.SelectedIndex = langIndex;
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
            btnScan.Top = grpDevices.Height - btnScan.Height - 10;
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

            if (cmbLanguage.SelectedItem!.ToString() == Resource.Get("Language", CultureInfo.GetCultureInfo("en")))
                SetCulture("en");
            else if (cmbLanguage.SelectedItem.ToString() == Resource.Get("Language", CultureInfo.GetCultureInfo("fr")))
                SetCulture("fr");
            else if (cmbLanguage.SelectedItem.ToString() == Resource.Get("Language", CultureInfo.GetCultureInfo("de")))
                SetCulture("de");
        }

        public void SetCulture(string culture)
        {
            if (applicationLogic == null)
                return;

            CultureInfo ci = new CultureInfo(culture);
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
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
            if (logDeviceCommunication)
            {
                if (!tabControl.TabPages.Contains(tabPageLog))
                    tabControl.TabPages.Add(tabPageLog);
            }
            else
            {
                if (tabControl.TabPages.Contains(tabPageLog))
                    tabControl.TabPages.Remove(tabPageLog);
            }
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
                case TabControl tc:
                    tc.BackColor = ink; tc.ForeColor = text;
                    break;
                case TabPage tp:
                    tp.BackColor = ink; tp.ForeColor = text;
                    break;
                case GroupBox gb:
                    gb.BackColor = ink; gb.ForeColor = subtle;
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
                case ComboBox cb:
                    cb.BackColor = surface; cb.ForeColor = text; cb.FlatStyle = FlatStyle.Flat;
                    break;
                case FlowLayoutPanel flp when flp.Parent is not DeviceControl:
                    flp.BackColor = ink; flp.ForeColor = text;
                    break;
                case Panel pnl when pnl.Parent is not DeviceControl:
                    pnl.BackColor = ink; pnl.ForeColor = text;
                    break;
                case Button btn when btn.Parent is not DeviceControl:
                    btn.BackColor = surface; btn.ForeColor = text;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Classes.Theme.Line;
                    btn.FlatAppearance.MouseOverBackColor = darkmode ? Classes.Theme.Raised : SystemColors.ControlLight;
                    btn.UseVisualStyleBackColor = false;
                    break;
                case Label lbl when lbl.Parent is not DeviceControl && lbl.Name != "lblSectionSound" && lbl.Name != "lblSectionBehavior":
                    lbl.BackColor = Color.Transparent; lbl.ForeColor = subtle;
                    break;
            }
        }

        private UserControls.AppHeaderControl? headerControl;

        /// <summary>
        /// Applies the premium "hi-fi console" chrome: the branded header band above the master-volume bar, and
        /// owner-drawn group boxes (the stock etched border is over-drawn so the dark theme reads cleanly).
        /// </summary>
        private void SetupPremiumChrome()
        {
            if (tabPageMain == null || headerControl != null)
                return;

            headerControl = new UserControls.AppHeaderControl();
            tabPageMain.Controls.Add(headerControl);  // last-added => top-most Dock=Top, sits above grpVolume

            ThemeGroupBoxBorder(grpVolume);
            ThemeGroupBoxBorder(grpDevices);
            ThemeGroupBoxBorder(grpLag);
            ThemeGroupBoxBorder(grpOptions);
            pnlDevices.BackColor = Classes.Theme.Ink;
            pnlDevices.Paint += PnlDevices_PaintRingWatermark;
            SetupComboBoxDarkDraw(this);

            var icon = Classes.Theme.LoadAppIcon();
            if (icon != null) Icon = icon;   // amber-ring brand mark on the title bar + taskbar

            if (volumeMeter != null) { volumeMeter.BackColor = Classes.Theme.Ink2; volumeMeter.ForeColor = Classes.Theme.Amber; }
            // The concept has no global VU meter — the live level lives on each card (the card's signature).
            // Retire the header meter + its "dB" label so the top row reads as one calm strip.
            if (pnlVolumeMeter != null) pnlVolumeMeter.Visible = false;

            SetupMasterVolumeFader();
            SetupRoomSummary();
            SetupBrandCredit();
            SetupTabStrip();
            SetupOptionsSections();
        }

        private System.Windows.Forms.Timer? roomSummaryTimer;
        private Label? lblRoomSummaryTitle, lblRoomSummarySubtitle;

        // The concept places a live "N Geräte · M spielen" / "format · verlustfrei ans ganze Haus" summary
        // to the LEFT of the master-volume fader, replacing the generic "ALLE RÄUME" group caption. Two
        // stacked labels (different weights match the concept's title/subtitle) inserted before the fader.
        private void SetupRoomSummary()
        {
            if (pnlVolumeAllButtons == null || pnlVolumeAllButtons.Controls.ContainsKey("pnlRoomSummary"))
                return;

            grpVolume.Text = string.Empty; // no more generic uppercase caption on this row

            lblRoomSummaryTitle = new Label
            {
                AutoSize = true,
                Font = new Font(Classes.Theme.Name.FontFamily, 11.5f, Classes.Theme.Name.Style),
                ForeColor = Classes.Theme.Ivory,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
            };
            lblRoomSummarySubtitle = new Label
            {
                AutoSize = true,
                Font = Classes.Theme.Small,
                ForeColor = Classes.Theme.Slate,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
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
            pnlVolumeAllButtons.Controls.Add(stack);
            pnlVolumeAllButtons.Controls.SetChildIndex(stack, 0);

            RefreshRoomSummary();
            roomSummaryTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            roomSummaryTimer.Tick += (s, e) => RefreshRoomSummary();
            roomSummaryTimer.Start();
        }

        private void RefreshRoomSummary()
        {
            if (lblRoomSummaryTitle == null || lblRoomSummarySubtitle == null) return;
            var (total, playing) = CountDevices();
            // Unified vocabulary: "Räume" everywhere (matches the tabs), with correct singular ("1 Raum",
            // "1 spielt").
            string count = total == 1 ? "1 Raum" : $"{total} Räume";
            string verb = playing == 1 ? "spielt" : "spielen";
            lblRoomSummaryTitle.Text = playing > 0 ? $"{count} · {playing} {verb}" : count;
            lblRoomSummarySubtitle.Text = string.Format(Properties.Strings.Label_RoomSummarySubtitle_Text, Classes.Theme.CurrentFormatLabel);
        }

        // Groups the flat Einstellungen list into two labelled sections. Both panels are plain Dock=Top
        // stacks, so a header appended at runtime (Controls.Add always adds to the END of the collection,
        // and WinForms docks Top-siblings in REVERSE collection order - last added = outermost/topmost)
        // lands exactly above the existing rows without touching any of their designer-computed layout.
        private void SetupOptionsSections()
        {
            if (pnlOptions == null || pnlOptions.Controls.ContainsKey("lblSectionSound"))
                return;

            pnlOptions.Controls.Add(SectionDivider());
            pnlOptions.Controls.Add(SectionHeader("lblSectionSound", Properties.Strings.Label_Section_Sound_Text));

            pnlOptionsCheckBoxes.Controls.Add(SectionDivider());
            pnlOptionsCheckBoxes.Controls.Add(SectionHeader("lblSectionBehavior", Properties.Strings.Label_Section_Behavior_Text));
        }

        private static Label SectionHeader(string name, string text) => new Label
        {
            Name = name,
            Text = text,
            UseMnemonic = false, // otherwise "&" in the title (e.g. "Klangprofil & Verbindung") is eaten as an accelerator marker
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 10, 0, 4),
            Font = new Font(Classes.Theme.Label.FontFamily, 8.5f, FontStyle.Bold),
            ForeColor = Classes.Theme.Amber,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.BottomLeft,
        };

        private static Panel SectionDivider() => new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            Margin = new Padding(0),
            BackColor = Classes.Theme.Line,
        };

        // The native TabControl header ignores BackColor/ForeColor (OS visual-styles draw it), which is why the
        // tab strip kept its light chrome even after the rest of the app went dark. Owner-draw it as flat,
        // borderless pills with an amber underline on the active tab, per the approved UI concept.
        private void SetupTabStrip()
        {
            if (tabControl == null || tabControl.DrawMode == TabDrawMode.OwnerDrawFixed)
                return;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.Padding = new Point(18, 6);
            tabControl.ItemSize = new Size(128, 34);
            tabControl.DrawItem += TabControl_DrawItem;
            Classes.DwmChrome.DisableVisualStyles(tabControl); // stop the native tab-row chrome bleeding through
            if (txtLog != null) Classes.DwmChrome.DisableVisualStyles(txtLog); // dark scrollbar instead of a light native one
        }

        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            bool dark = GetDarkMode();
            var tab = tabControl.TabPages[e.Index];
            bool active = e.Index == tabControl.SelectedIndex;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var bg = dark ? (active ? Classes.Theme.Ink : Classes.Theme.Ink2) : (active ? SystemColors.Control : SystemColors.ControlLight);
            var fg = dark ? (active ? Classes.Theme.Ivory : Classes.Theme.Slate) : (active ? Color.Black : SystemColors.GrayText);
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);

            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using var tabFont = new Font(Classes.Theme.Name.FontFamily, 9.5f, FontStyle.Bold);
            using (var fb = new SolidBrush(fg)) g.DrawString(tab.Text, tabFont, fb, e.Bounds, sf);

            if (active)
            {
                var underline = dark ? Classes.Theme.Amber : SystemColors.Highlight;
                using var pen = new Pen(underline, 2.4f);
                g.DrawLine(pen, e.Bounds.Left + 6, e.Bounds.Bottom - 2, e.Bounds.Right - 6, e.Bounds.Bottom - 2);
            }

            // The fixed-width tab strip rarely fills the control's whole width - the leftover band to the right
            // of the last tab isn't covered by any DrawItem call and would otherwise show the native light
            // tab-row background. Paint over it once, right after the last tab.
            if (e.Index == tabControl.TabCount - 1 && e.Bounds.Right < tabControl.Width)
            {
                var rest = new Rectangle(e.Bounds.Right, e.Bounds.Top, tabControl.Width - e.Bounds.Right, e.Bounds.Height);
                using var rb = new SolidBrush(dark ? Classes.Theme.Ink : SystemColors.Control);
                g.FillRectangle(rb, rest);
            }
        }

        // A small, discreet brand credit - not legally required (see docs/THIRD-PARTY-LICENSES.md and
        // README.md for the formal MIT attribution to the original author), just a quiet personal signature.
        private void SetupBrandCredit()
        {
            if (grpOptions == null || grpOptions.Controls.ContainsKey("lblCredit"))
                return;

            var credit = new Label
            {
                Name = "lblCredit",
                Text = Properties.Strings.Label_Credit_Text,
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(10, 2, 3, 6),
                Font = new Font(Classes.Theme.Small.FontFamily, 8f, FontStyle.Italic),
                ForeColor = Classes.Theme.Slate2,
                BackColor = Color.Transparent,
            };
            grpOptions.Controls.Add(credit);

            if (headerControl != null)
            {
                toolTipGroup2 ??= new ToolTip();
                toolTipGroup2.SetToolTip(headerControl, Properties.Strings.Label_Credit_Text);
            }
        }

        private ToolTip? toolTipGroup2;

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

            var master = new UserControls.MasterVolumeControl { Name = "masterVolume", Margin = new Padding(3, 10, 12, 4) };
            master.VolumeDragged += t => { foreach (Control c in pnlDevices.Controls) if (c is UserControls.DeviceControl dc) dc.ApplyVolumeAbsolute(t); };
            master.MuteClicked += () => devices?.VolumeMute();
            pnlVolumeAllButtons.Controls.Add(master);
            pnlVolumeAllButtons.Controls.SetChildIndex(master, 0);
        }

        // A faint concentric-ring watermark bleeding off the top-right corner - echoes the brand's amber "sound
        // rings" (the TV artwork / app logo) behind the device grid, per the approved UI concept. Child cards
        // paint over it since they're separate child windows, so it only shows through the gaps.
        private void PnlDevices_PaintRingWatermark(object? sender, PaintEventArgs e)
        {
            if (!GetDarkMode()) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = pnlDevices.ClientSize.Width - 20, cy = -60;
            DrawRing(g, cx, cy, 90, 26);
            DrawRing(g, cx, cy, 160, 16);
            DrawRing(g, cx, cy, 230, 9);
        }

        private static void DrawRing(Graphics g, float cx, float cy, float r, int alpha)
        {
            using var pen = new Pen(Color.FromArgb(alpha, Classes.Theme.Amber), 1.4f);
            g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
        }

        // WinForms DropDownList combos ignore BackColor, so owner-draw them for the dark theme.
        private void SetupComboBoxDarkDraw(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is ComboBox cb)
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
            var bg = dark ? (selected ? Classes.Theme.Raised : Classes.Theme.Surface) : (selected ? SystemColors.Highlight : SystemColors.Window);
            var fg = dark ? Classes.Theme.Ivory : (selected ? SystemColors.HighlightText : SystemColors.ControlText);
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index >= 0)
                TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font,
                    new Point(e.Bounds.X + 2, e.Bounds.Y + 1), fg);
        }

        private (int total, int playing) CountDevices()
        {
            int total = 0, playing = 0;
            if (pnlDevices != null)
                foreach (var c in pnlDevices.Controls)
                    if (c is UserControls.DeviceControl dc) { total++; if (dc.IsPlaying) playing++; }
            return (total, playing);
        }

        // Over-draw the stock GroupBox etched border with Ink (children paint themselves on top) and re-draw the
        // caption as a quiet uppercase slate label - so the box reads as a clean section, not a 3D frame.
        private void ThemeGroupBoxBorder(GroupBox gb)
        {
            if (gb == null) return;
            gb.Paint += (s, e) =>
            {
                if (!GetDarkMode()) return;
                var g = e.Graphics;
                using (var b = new SolidBrush(Classes.Theme.Ink))
                {
                    g.FillRectangle(b, 0, 0, gb.Width, 15);
                    g.FillRectangle(b, 0, 0, 3, gb.Height);
                    g.FillRectangle(b, gb.Width - 3, 0, 3, gb.Height);
                    g.FillRectangle(b, 0, gb.Height - 3, gb.Width, 3);
                }
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
            const int cellW = 322 + 14, cellH = 152 + 14;   // card + margins
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
