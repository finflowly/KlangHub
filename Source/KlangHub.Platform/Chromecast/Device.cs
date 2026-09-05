using System;
using System.Linq;
using System.Net.Sockets;
using NAudio.Wave;
using KlangHub.Communication;
using KlangHub.Communication.Classes;
using KlangHub.Streaming.Interfaces;
using KlangHub.Communication.Interfaces;
using KlangHub.Classes;
using KlangHub.Streaming;
using KlangHub.ProtocolBuffer;
using KlangHub.Discover;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using KlangHub.Platform.Casting.Chromecast;
using PlaybackState = KlangHub.Core.Casting.PlaybackState;   // disambiguate from NAudio.Wave.PlaybackState

namespace KlangHub.Application
{
    /// <summary>
    /// Device represents a Chromecast device, or a Chromecast group.
    /// </summary>
    public class Device : IDevice, IPlaybackSession
    {
        private readonly IDeviceCommunication deviceCommunication;
        private IStreamingConnection? streamingConnection;
        private readonly IDeviceConnection deviceConnection;
        private readonly DiscoveredDevice discoveredDevice;
        private Volume volumeSetting;
        private DateTime latestVolumeChange;
        private float latestVolumeSet;
        private readonly ILogger logger;
        private DateTime lastGetStatus;
        private bool devicePlayedWhenStopped;
        private bool wasPlayingWhenConnectError;
        private DeviceEureka eureka = null!;
        private Action<DeviceEureka> setDeviceInformationCallback = null!;
        private Action<Action, CancellationTokenSource?> startTask = null!;
        //private Action<IDevice> stopGroup;
        private Func<IDevice, bool> isGroupStatusBlank = null!;
        private Action<bool> autoMute = null!;
        private DateTime? lastLoadMessageTime;
        private DateTime? addStreamingConnectionTime;

        // ConnectError circuit-breaker: a device stuck unreachable (powered off / moved to a new IP) otherwise
        // hammers two 5s blocking timeouts (TLS GET_STATUS + eureka fetch) on every 15s poll forever. Back the
        // ConnectError poll off 15 -> 30 -> 60s; healthy devices poll at the normal cadence (see ShouldRunPoll).
        private readonly BackoffPolicy reconnectBackoff = new BackoffPolicy(baseSeconds: 15.0, maxSeconds: 60.0);
        private DateTime nextReconnectAttempt = DateTime.MinValue;

        private bool isDisposed;

        delegate void SetDeviceStateCallback(DeviceState state, string? text = null);

        public Device(ILogger loggerIn, ICastHost applicationLogicIn)
        {
            logger = loggerIn;
            deviceConnection = new DeviceConnection(logger);
            deviceCommunication = new DeviceCommunication(applicationLogicIn, logger);
            deviceConnection.SetCallback(GetHost, GetPort, SetDeviceState, OnReceiveMessage, StartTask);
            discoveredDevice = new DiscoveredDevice
            {
                DeviceState = DeviceState.NotConnected
            };
            volumeSetting = new Volume
            {
                controlType = "attenuation",
                level = 0.0f,
                muted = false,
                stepInterval = 0.05f
            };
        }

        /// <summary>
        /// Initialize a device.
        /// </summary>
        /// <param name="discoveredDeviceIn">the discovered device</param>
        public void Initialize(DiscoveredDevice discoveredDeviceIn, Action<DeviceEureka> setDeviceInformationCallbackIn
            , Action<IDevice> stopGroupIn, Action<Action, CancellationTokenSource?> startTaskIn, Func<IDevice, bool> isGroupStatusBlankIn
            , Action<bool> autoMuteIn)
        {
            setDeviceInformationCallback = setDeviceInformationCallbackIn;
            //stopGroup = stopGroupIn;
            startTask = startTaskIn;
            isGroupStatusBlank = isGroupStatusBlankIn;
            autoMute = autoMuteIn;

            if (discoveredDevice == null || deviceCommunication == null || deviceConnection == null ||
                discoveredDeviceIn == null || setDeviceInformationCallbackIn == null || stopGroupIn == null || isDisposed)
                return;

            var ipChanged = discoveredDevice.IPAddress != discoveredDeviceIn.IPAddress;

            // Logging
            if (ipChanged ||
                discoveredDevice.Name != discoveredDeviceIn.Name ||
                discoveredDevice.Port != discoveredDeviceIn.Port ||
                JsonSerializer.Serialize(discoveredDevice.Eureka?.Multizone?.Groups)
                    != JsonSerializer.Serialize(discoveredDeviceIn.Eureka?.Multizone?.Groups)
               )
            {
                logger.Log($"Discovered device: {discoveredDeviceIn?.Name} {discoveredDeviceIn?.IPAddress}:{discoveredDeviceIn?.Port} {JsonSerializer.Serialize(discoveredDeviceIn?.Eureka?.Multizone?.Groups)} {discoveredDeviceIn?.Id}");
            }

            if (discoveredDeviceIn!.Headers != null) discoveredDevice.Headers = discoveredDeviceIn.Headers;
            if (discoveredDeviceIn.IPAddress != null) discoveredDevice.IPAddress = discoveredDeviceIn.IPAddress;
            if (discoveredDeviceIn.MACAddress != null) discoveredDevice.MACAddress = discoveredDeviceIn.MACAddress;
            if (discoveredDeviceIn.Id != null) discoveredDevice.Id = discoveredDeviceIn.Id;
            if (discoveredDeviceIn.Name != null) discoveredDevice.Name = discoveredDeviceIn.Name;
            if (discoveredDeviceIn.Port != 0) discoveredDevice.Port = discoveredDeviceIn.Port;
            if (discoveredDeviceIn.Protocol != null) discoveredDevice.Protocol = discoveredDeviceIn.Protocol;
            if (discoveredDeviceIn.Usn != null) discoveredDevice.Usn = discoveredDeviceIn.Usn;
            discoveredDevice.AddedByDeviceInfo = discoveredDeviceIn.AddedByDeviceInfo;
            if (discoveredDeviceIn.Eureka != null) discoveredDevice.Eureka = discoveredDeviceIn.Eureka;
            if (discoveredDeviceIn.Group != null) discoveredDevice.Group = discoveredDeviceIn.Group;

            // Accumulate every address this device has been keyed at (mDNS per-family + eureka self-report) so a
            // stream socket connecting back from any of them attaches (see MatchesAddress).
            MergeAddress(discoveredDevice.IPAddress);
            if (discoveredDeviceIn.Addresses != null)
                foreach (var a in discoveredDeviceIn.Addresses)
                    MergeAddress(a);

            deviceCommunication.SetCallback(this, deviceConnection.SendMessage, deviceConnection.IsConnected);
            if (ipChanged && GetDeviceState() == DeviceState.Playing)
            {
                ResumePlaying();
            }
        }

        /// <summary>
        /// Set the device information.
        /// </summary>
        /// <param name="eureka"></param>
        private void SetDeviceInformation(DeviceEureka eurekaIn)
        {
            eureka = eurekaIn;
            setDeviceInformationCallback?.Invoke(eureka);
        }

        /// <summary>
        /// Play/Pause button is clicked.
        /// </summary>
        public void OnClickPlayStop()
        {
            if (deviceCommunication == null || isDisposed)
                return;

            // Disabled because group information isn't available since a firmware update.
            //stopGroup(this);
            deviceCommunication.OnPlayStop_Click();
            lastGetStatus = DateTime.Now;
            autoMute(deviceCommunication.GetUserMode() == UserMode.Playing);
        }

        public void OnClickPlayPause(object sender, EventArgs e)
        {
            OnClickPlayStop();
        }

        /// <summary>
        /// Load the stream on the device.
        /// </summary>
        public void Start()
        {
            if (devicePlayedWhenStopped || isDisposed)
            {
                ResumePlaying();
            }
        }

        /// <summary>
        /// Stream the recorded data to the device.
        /// </summary>
        /// <param name="dataToSend">tha audio data</param>
        /// <param name="format">the wav format</param>
        /// <param name="reduceLagThreshold">lag value</param>
        /// <param name="streamFormat">the stream format</param>
        public void OnRecordingDataAvailable(byte[] dataToSend, AudioFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat)
        {
            if (streamingConnection == null || dataToSend == null || dataToSend.Length == 0 || isDisposed)
                return;

            if (streamingConnection.IsConnected())
            {
                if (GetDeviceState() != DeviceState.NotConnected &&
                    GetDeviceState() != DeviceState.Paused) // When you keep streaming to a device when it is paused, the application stops streaming after a while (local buffers full?)
                {
                    streamingConnection.SendData(dataToSend, format, reduceLagThreshold, streamFormat);
                }
            }
            else
            {
                logger.Log($"Connection closed from {streamingConnection.GetRemoteEndPoint()} after {streamingConnection.Carried()}");
                NoteRepeatedDrop();
                // Disposed, not just dropped. Dispose sets LingerState(true, 0) so the socket is reset
                // rather than closed gracefully - without it, bytes still queued can be read by a
                // keep-alive receiver as the headers of whatever comes next, and the rebuild below opens
                // the next connection into exactly that state.
                streamingConnection.Dispose();
                streamingConnection = null;

                // The other half of yesterday's fix. That one caught a FAILED SEND; this catches the case
                // where the receiver closed the audio socket itself - measured 2026-09-05 08:45:30, when a
                // soundbar answered with detailedErrorCode 102 and hung up. Nothing asked for a rebuild, so
                // it took 23 seconds instead of six. The gate inside keeps this from racing the poll.
                ResumeAfterConnectionLoss();
            }
        }

        private int dropCount;

        /// <summary>
        /// Names the pattern once it is a pattern.
        ///
        /// A receiver that stops on its own is normal enough to happen to anyone; a receiver that does it
        /// again and again while another speaker reads the SAME bytes without trouble is a property of that
        /// device, and the person reading the log should be told so rather than left to count the entries.
        /// Measured here on 2026-09-05: an Enchant gave up three times in an hour after 6, 18 and 15
        /// minutes, while a soundbar ran 55 minutes on one connection.
        /// </summary>
        private void NoteRepeatedDrop()
        {
            dropCount++;
            if (dropCount != 3)
                return;   // said once, at the point it stops being a coincidence

            logger.Log($"[{GetHost()}] has now dropped the audio stream {dropCount} times this session. " +
                       "If other speakers are playing the same stream without trouble, this is the device, " +
                       "not the network - a lighter format (16-bit, or a lower buffer) is the thing to try.");
        }

        /// <summary>
        /// Passes what is playing on to the stage on this device's screen. Silent for a device that is
        /// running Google's receiver, or one with no screen at all - a speaker is not a failure here.
        /// </summary>
        public void SendStageUpdate(KlangHub.Core.NowPlaying.StageUpdate update)
        {
            if (deviceCommunication == null || isDisposed)
                return;

            // The zone is filled in here rather than by the sender: one message goes out to every device,
            // but each stage names the room it is standing in. "Wohnzimmer" on the television in the
            // living room, whatever the kitchen speaker is called on the kitchen one.
            deviceCommunication.SendStageUpdate(update with { Zone = GetFriendlyName() });
        }

        public void SendStageState(bool playing)
        {
            if (deviceCommunication == null || isDisposed)
                return;

            deviceCommunication.SendStageState(playing);
        }

        /// <summary>
        /// Get the device status.
        /// </summary>
        public void OnGetStatus()
        {
            if (deviceCommunication == null || isDisposed)
                return;

            DoFirewallCheck();

            if (GetDeviceState() != DeviceState.Disposed && (DateTime.Now - lastGetStatus).TotalSeconds > 5)
            {
                // Throttle only the ConnectError case (dead/moved device) so it doesn't spam two 5s timeouts
                // every poll; a device on any other state polls exactly as before.
                if (!ShouldRunPoll(GetDeviceState() == DeviceState.ConnectError, DateTime.Now, ref nextReconnectAttempt, reconnectBackoff))
                    return;

                deviceCommunication.GetStatus();
                GetDeviceInformation();
                lastGetStatus = DateTime.Now;
            }
        }

        /// <summary>Poll gate for the ConnectError circuit-breaker (pure + testable). A device NOT in ConnectError
        /// always polls and resets the backoff. A ConnectError device polls at most once per backoff window
        /// (15 -> 30 -> 60s), skipping in between - so a dead/moved device stops hammering blocking timeouts.</summary>
        internal static bool ShouldRunPoll(bool isConnectError, DateTime now, ref DateTime nextAttempt, BackoffPolicy backoff)
        {
            if (!isConnectError)
            {
                backoff.Reset();
                nextAttempt = DateTime.MinValue;
                return true;
            }

            if (now < nextAttempt)
                return false;

            nextAttempt = now + backoff.NextDelay();
            return true;
        }

        /// <summary>
        /// Set the device status.
        /// </summary>
        /// <param name="state">the state</param>
        /// <param name="statusText">status text</param>
        public void SetDeviceState(DeviceState state, string? statusText = null)
        {
            if (discoveredDevice == null || isDisposed)
                return;

            // 2.2b-4.7: no UI marshaling here anymore. The state-machine logic runs on the caller's
            // (comm) thread and StateChanged is raised; observers (DeviceControl, tray) self-marshal.

            // Restart when recovering from a connection error.
            if (state != DeviceState.ConnectError && wasPlayingWhenConnectError)
            {
                wasPlayingWhenConnectError = false;
                ResumePlaying();
            }
            else if (state == DeviceState.ConnectError && GetDeviceState() == DeviceState.Playing)
            {
                wasPlayingWhenConnectError = true;
            }

            DoFirewallCheckSaveTimes(state);

            discoveredDevice.DeviceState = state;
            StateChanged?.Invoke(this, ChromecastStateMapper.ToPlaybackState(state));
        }

        /// <summary>
        /// Set the volume on the device
        /// </summary>
        /// <param name="level"></param>
        public void VolumeSet(float level)
        {
            if (deviceCommunication == null || volumeSetting == null || isDisposed)
                return;

            if (DateTime.Now.Ticks - latestVolumeChange.Ticks < 1000)
                return;

            latestVolumeChange = DateTime.Now;

            if (volumeSetting.level > level)
                while (volumeSetting.level > level) volumeSetting.level -= volumeSetting.stepInterval;
            if (volumeSetting.level < level)
                while (volumeSetting.level < level) volumeSetting.level += volumeSetting.stepInterval;
            if (level > 1) { level = 1; volumeSetting.level = level; }
            if (level < 0) { level = 0; volumeSetting.level = level; }

            deviceCommunication.VolumeSet(volumeSetting);
            latestVolumeSet = level;
        }

        /// <summary>
        /// Volume up.
        /// </summary>
        public void VolumeUp()
        {
            if (volumeSetting == null || isDisposed)
                return;

            VolumeSet(volumeSetting.level + 0.05f);
        }

        /// <summary>
        /// Volume down.
        /// </summary>
        public void VolumeDown()
        {
            if (volumeSetting == null || isDisposed)
                return;

            VolumeSet(volumeSetting.level - 0.05f);
        }

        /// <summary>
        /// Volume mute.
        /// </summary>
        public void VolumeMute()
        {
            if (deviceCommunication == null || volumeSetting == null || isDisposed)
                return;

            deviceCommunication.VolumeMute(!volumeSetting.muted);
        }

        /// <summary>
        /// Stop playing on the device.
        /// </summary>
        public void Stop(bool changeUserMode = false)
        {
            if (deviceCommunication == null || isDisposed)
                return;

            switch (GetDeviceState())
            {
                case DeviceState.Playing:
                case DeviceState.LoadingMedia:
                case DeviceState.LoadingMediaCheckFirewall:
                case DeviceState.Buffering:
                case DeviceState.Paused:
                    devicePlayedWhenStopped = GetDeviceState() == DeviceState.Playing;
                    deviceCommunication.Stop(changeUserMode);
                    SetDeviceState(DeviceState.Closed);
                    break;
                default:
                    break;
            }
            autoMute(deviceCommunication.GetUserMode() == UserMode.Playing);
        }

        /// <summary>
        /// Add the streaming connection if it's for this device.
        /// </summary>
        /// <param name="remoteAddress">remote IP address of the streaming connection</param>
        /// <param name="socket">socket of the streaming connection</param>
        /// <returns></returns>
        public bool AddStreamingConnection(string remoteAddress, Socket socket, SupportedStreamFormat streamFormat)
        {
            if (discoveredDevice == null || isDisposed)
                return false;

            addStreamingConnectionTime = DateTime.Now;

            //TODO: Is this right for device groups?
            if ((GetDeviceState() == DeviceState.LoadingMedia ||
                GetDeviceState() == DeviceState.LoadingMediaCheckFirewall ||
                GetDeviceState() == DeviceState.Buffering ||
                GetDeviceState() == DeviceState.Idle) &&
                MatchesAddress(remoteAddress))
            {
                // Built fully, then published. Assigning the field first left a window in which the capture
                // thread - which runs about fifty times a second - could see a connection whose socket was
                // not attached yet, read IsConnected() as false, log "Connection closed from " with no
                // endpoint to name, null the field and (since the fast-recovery change) tear down and
                // relaunch the whole Cast session. SetDependencies then finished wiring an object nobody
                // held any more: a silent speaker and a spurious LAUNCH, once per reconnect.
                var connection = new StreamingConnection();
                connection.SetDependencies(socket, this, logger);
                connection.SendStartStreamingResponse(streamFormat);
                streamingConnection = connection;
                return true;
            }

            return false;
        }

        /// <summary>True if the streaming socket's remote address is one this device is known at. The stream is
        /// pulled back over IPv4, but a device discovered over IPv6 is keyed on an IPv6 literal; matching the
        /// accumulated address set (scope-normalized) lets the IPv4 return-socket still attach. Falls back to
        /// the primary IPAddress so today's IPv4-only devices are unaffected.</summary>
        private bool MatchesAddress(string remoteAddress)
        {
            if (discoveredDevice == null)
                return false;

            var normalized = Ipv4Recovery.Normalize(remoteAddress);
            if (Ipv4Recovery.Normalize(discoveredDevice.IPAddress) == normalized)
                return true;

            return discoveredDevice.Addresses != null
                && discoveredDevice.Addresses.Any(a => Ipv4Recovery.Normalize(a) == normalized);
        }

        /// <summary>Accumulate an address this device has been seen at (scope-normalized dedup), so the streaming
        /// set-match knows every family/interface the device may connect back from.</summary>
        private void MergeAddress(string? address)
        {
            if (discoveredDevice == null || string.IsNullOrEmpty(address))
                return;

            var normalized = Ipv4Recovery.Normalize(address);
            if (!discoveredDevice.Addresses.Any(a => Ipv4Recovery.Normalize(a) == normalized))
                discoveredDevice.Addresses.Add(address!);
        }

        /// <summary>
        /// Get the Usn of the device.
        /// </summary>
        public string GetUsn()
        {
            if (discoveredDevice == null || isDisposed)
                return string.Empty;

            return discoveredDevice.Usn;
        }

        /// <summary>
        /// Get the host of the device.
        /// </summary>
        public string GetHost()
        {
            if (discoveredDevice == null || isDisposed)
                return string.Empty;

            return discoveredDevice.IPAddress;
        }

        /// <summary>
        /// Get the friendly name of the device.
        /// </summary>
        /// <returns></returns>
        public string GetFriendlyName()
        {
            if (discoveredDevice == null || isDisposed)
                return string.Empty;

            return discoveredDevice.Name;
        }

        /// <summary>
        /// Returns the device state.
        /// </summary>
        public DeviceState GetDeviceState()
        {
            if (discoveredDevice == null || isDisposed)
                return DeviceState.Disposed;

            return discoveredDevice.DeviceState;
        }

        /// <summary>
        /// Returns the connection for the control messages.
        /// </summary>
        public IDeviceConnection GetDeviceConnection()
        {
            return deviceConnection;
        }

        /// <summary>
        /// A message from the device is received.
        /// </summary>
        public void OnReceiveMessage(CastMessage castMessage)
        {
            if (deviceCommunication == null || isDisposed)
                return;

            deviceCommunication.OnReceiveMessage(castMessage);
        }

        /// <summary>
        /// Get the port of the device.
        /// </summary>
        /// <returns>the port of the device, or 0</returns>
        public int GetPort()
        {
            if (discoveredDevice == null || isDisposed)
                return 0;

            return discoveredDevice.Port;
        }

        /// <summary>
        /// Return the discovered device.
        /// </summary>
        public DiscoveredDevice GetDiscoveredDevice()
        {
            return discoveredDevice;
        }

        /// <summary>
        /// Determine if this is a Chromecast group.
        /// </summary>
        /// <returns>true if it's a group, false if it's not a group</returns>
        public bool IsGroup()
        {
            if (discoveredDevice == null || isDisposed)
                return false;

            return discoveredDevice.IsGroup;
        }

        /// <summary>
        /// Determine if the device is in a connected state.
        /// </summary>
        /// <returns>true if it's connected, or false if not</returns>
        public bool IsConnected()
        {
            return !(GetDeviceState().Equals(DeviceState.NotConnected) ||
                GetDeviceState().Equals(DeviceState.ConnectError) ||
                GetDeviceState().Equals(DeviceState.Closed));
        }

        /// <summary>
        /// A volume update from the device.
        /// </summary>
        /// <param name="volume">the volume on the device</param>
        public void OnVolumeUpdate(Volume volume)
        {
            if (isDisposed)
                return;

            var tmpLevel = volume.level;
            volumeSetting = volume;
            if (volume.level != latestVolumeSet && latestVolumeSet != 0)
            {
                volume.level = latestVolumeSet;
                if (LevelIsOk(tmpLevel))
                {
                    latestVolumeSet = 0;
                }
            }
            else if (LevelIsOk(tmpLevel))
            {
                latestVolumeSet = 0;
            }

            // 2.2b-4.5: DeviceControl observes volume via VolumeChanged (no direct push).
            VolumeChanged?.Invoke(this, new VolumeStatus(volumeSetting.level, volumeSetting.muted, volumeSetting.stepInterval));
        }

        private bool LevelIsOk(float level)
        {
            return Math.Abs(level - latestVolumeSet) <= volumeSetting.stepInterval;
        }

        /// <summary>
        /// Send silence to the device.
        /// </summary>
        public void SendSilence()
        {
            var silence = new WavGenerator().GetSilenceBytes(5);
            OnRecordingDataAvailable(silence, new AudioFormat(44100, 16, 2), 1000, SupportedStreamFormat.Mp3_320);
        }

        /// <summary>Keeps the periodic eureka refresh from running on every single poll - see
        /// <see cref="GetDeviceInformation"/>.</summary>
        private readonly DiscoveryThrottle deviceInformationThrottle =
            new(System.TimeSpan.FromMinutes(2));

        /// <summary>
        /// Re-read the device's own description (its name, address and MAC).
        ///
        /// This used to run on EVERY status poll: four devices meant four HTTP requests to :8008 every
        /// fifteen seconds, forever, including while those devices were decoding audio - 240 requests an
        /// hour per speaker to learn a name that practically never changes. Twice a minute is plenty:
        /// a device that is genuinely renamed or moved announces itself over mDNS, and THAT path refetches
        /// immediately (see Devices.OnDeviceAvailable).
        /// </summary>
        private void GetDeviceInformation()
        {
            if (IsGroup())
                return;

            var key = discoveredDevice.Id;
            if (string.IsNullOrEmpty(key))
                key = $"{discoveredDevice.IPAddress}:{discoveredDevice.Port}";
            if (!deviceInformationThrottle.ShouldAct(key, null))
                return;

            startTask(DeviceInformation.GetDeviceInformation(discoveredDevice, SetDeviceInformation, null, logger), null);
        }

        /// <summary>
        /// Resume playing.
        /// </summary>
        public void ResumeAfterConnectionLoss()
        {
            if (isDisposed)
                return;

            deviceCommunication.ResumeAfterConnectionLoss();
        }

        public void ResumePlaying()
        {
            if (deviceCommunication == null || isDisposed)
                return;

            deviceCommunication.ResumePlaying();
            autoMute(deviceCommunication.GetUserMode() == UserMode.Playing);
        }

        /// <summary>
        /// Return device eureka information.
        /// </summary>
        public DeviceEureka GetEureka()
        {
            return eureka;
        }

        /// <summary>
        /// Start a task
        /// </summary>
        public void StartTask(Action action)
        {
            if (startTask == null)
                Task.Run(action);
            else
                startTask(action, null);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            isDisposed = true;
            Stop();
            deviceCommunication?.Dispose();
            streamingConnection?.Dispose();
            deviceConnection?.Dispose();
        }

        /// <summary>
        /// Check if the status text of the device is empty,
        /// For groups the status text of all devices in the group should be empty.
        /// </summary>
        /// <returns>true if the status text(s) are empty, or false</returns>
        public bool IsStatusTextBlank()
        {
            if (IsGroup())
            {
                return isGroupStatusBlank(this);
            }
            else
            {
                var statusText = GetStatusText();
                return IsStatusTextBlankCheck(statusText);
            }
        }

        /// <summary>
        /// Get the status text returned by the device.
        /// </summary>
        /// <returns>the status text</returns>
        public string GetStatusText()
        {
            return deviceCommunication.GetStatusText();
        }

        /// <summary>
        /// Check if the status text of the device is blank.
        /// </summary>
        public bool IsStatusTextBlankCheck(string statusText)
        {
            return string.IsNullOrEmpty(statusText) || statusText?.IndexOf(Properties.Strings.ChromeCast_StreamTitle) >= 0;
        }

        /// <summary>
        /// Return the usermode.
        /// </summary>
        /// <returns>the usermode</returns>
        public UserMode GetUserMode()
        {
            return deviceCommunication.GetUserMode();
        }

        public int GetVolumeLevel()
        {
            return (int)Math.Round(volumeSetting.level * 100, 0);
        }

        /// <summary>
        /// Do a check if the firewall is closed.
        /// The device should create a streaming connection in 15 seconds after a LOAD message is send.
        /// </summary>
        private void DoFirewallCheck()
        {
            if (lastLoadMessageTime.HasValue)
            {
                if (addStreamingConnectionTime.HasValue)
                {
                    if ((addStreamingConnectionTime.Value - lastLoadMessageTime.Value).TotalSeconds > 15)
                        SetDeviceState(DeviceState.LoadingMediaCheckFirewall);
                }
                else
                {
                    if ((DateTime.Now - lastLoadMessageTime.Value).TotalSeconds > 15)
                        SetDeviceState(DeviceState.LoadingMediaCheckFirewall);
                }
            }
        }

        /// <summary>
        /// Save the time to do the firewall check later.
        /// </summary>
        /// <param name="state"></param>
        private void DoFirewallCheckSaveTimes(DeviceState state)
        {
            if (state == DeviceState.LoadingMedia)
            {
                lastLoadMessageTime = DateTime.Now;
                addStreamingConnectionTime = null;
            }
            else if (state != DeviceState.Idle &&
                state != DeviceState.Buffering)
            {
                lastLoadMessageTime = null;
            }
        }

        public bool IsDisposed()
        {
            return isDisposed;
        }

        public void CloseConnection()
        {
            deviceConnection.ReConnect();
        }

        // --- IPlaybackSession: neutral facade over this Chromecast device (Phase 2.2b) ----------
        // Purely additive. The events fire IN ADDITION to the existing DeviceControl callbacks, and
        // no neutral consumer is wired yet (ChromecastProvider.CreateSession is step 2.2b-3), so until
        // then these members are inert and change no existing behaviour.

        public event EventHandler<PlaybackState>? StateChanged;
        public event EventHandler<VolumeStatus>? VolumeChanged;

        CastDeviceDescriptor IPlaybackSession.Device =>
            new(ChromecastDeviceId.From(discoveredDevice), GetFriendlyName(), ProviderId.Chromecast, IsGroup())
            {
                Model = discoveredDevice?.ModelName,   // the card's subtitle: what hardware fills this room
                // The details card reads the session, not the discovery event, so the facts have to be
                // rebuilt here too - otherwise every speaker's details view showed its empty state while
                // DeviceFacts ran happily on the discovery side and its result was thrown away.
                Details = DeviceFacts.For(discoveredDevice),
            };

        PlaybackState IPlaybackSession.State => ChromecastStateMapper.ToPlaybackState(GetDeviceState());

        string IPlaybackSession.StatusText => GetStatusText() ?? string.Empty;

        VolumeStatus IPlaybackSession.Volume => new(volumeSetting?.level ?? 0f, volumeSetting?.muted ?? false, volumeSetting?.stepInterval ?? 0.05f);

        // 2.2b-M2: control methods are Task-returning on the contract; Chromecast's work is synchronous
        // (DeviceCommunication state machine), so each does its work and returns a completed Task.
        Task IPlaybackSession.Connect() { deviceCommunication.Connect(); return Task.CompletedTask; }
        Task IPlaybackSession.Play() { ResumePlaying(); return Task.CompletedTask; }
        Task IPlaybackSession.Pause() { deviceCommunication.PauseMedia(); return Task.CompletedTask; }
        Task IPlaybackSession.Stop() { Stop(true); return Task.CompletedTask; }
        // 1:1 delegation to the existing play/stop state machine (userMode flip + DeviceState-based
        // dispatch inside DeviceCommunication.OnPlayStop_Click). Behaviour-identical to the tray button.
        Task IPlaybackSession.TogglePlayStop() { OnClickPlayStop(); return Task.CompletedTask; }
        Task IPlaybackSession.SetVolume(float level) { VolumeSet(level); return Task.CompletedTask; }
        Task IPlaybackSession.SetMuted(bool muted) { deviceCommunication.VolumeMute(muted); return Task.CompletedTask; }
        Task IPlaybackSession.RequestStatus() { OnGetStatus(); return Task.CompletedTask; }
        Task IPlaybackSession.Disconnect() { deviceCommunication.Disconnect(); return Task.CompletedTask; }

        // 2.2b-4.4d (ownership): IPlaybackSession is IDisposable, but a Chromecast session is a NON-OWNING
        // control view over this shared, Devices-owned Device. Disposing a session must NOT tear the device
        // down - that would kill the connection, streaming and UI for every other holder. The real,
        // destructive teardown is the public Device.Dispose() (the IDevice slot), invoked only by Devices.
        // IDevice.Dispose() and IDisposable.Dispose() are distinct interface slots, so the public Dispose()
        // still serves IDevice unchanged.
        void IDisposable.Dispose() { /* no-op: non-owning session */ }
    }
}
