using System;
using System.Linq;
using KlangHub.Communication.Classes;
using KlangHub.ProtocolBuffer;
using KlangHub.Communication.Interfaces;
using System.Threading.Tasks;
using KlangHub.Application;
using KlangHub.Core.NowPlaying;
using System.Threading;
using System.Text.Json;

namespace KlangHub.Communication
{
    public class DeviceCommunication : IDeviceCommunication
    {
        private IDevice device = null!;
        private Action<byte[]> sendMessage = null!;
        private Func<bool> isDeviceConnected = null!;
        private readonly ICastHost applicationLogic;
        private readonly ILogger logger;
        private readonly IChromeCastMessages chromeCastMessages;
        private string chromeCastDestination;
        private readonly string chromeCastSource;
        private string chromeCastApplicationSessionNr = null!;
        private int chromeCastMediaSessionId;
        private int requestId;
        private VolumeSetItem? lastVolumeSetItem;
        private VolumeSetItem? nextVolumeSetItem;
        private bool Connected = false;
        private bool IsDisposed = false;
        private UserMode userMode = UserMode.Stopped;

        /// <summary>Keeps two rebuilds of the same session from running at once - see
        /// <see cref="ResumeAfterConnectionLoss"/>.</summary>
        private readonly ReconnectGate reconnectGate = new();
        private bool pendingStatusMessage = false;
        private DateTime lastReceivedMessage = DateTime.MinValue;
        private string? statusText;
        /// <summary>When the device said it was waiting for somebody to allow the launch, or null when it
        /// is not waiting. Read by <see cref="GetStatus"/> to end a wait nobody is going to answer.</summary>
        private DateTime? awaitingApprovalSince;
        // Reconnect backoff (PDF §3): grows the "still stuck launching" wait 5s -> 10 -> 20 -> 30 (+jitter) so a
        // device that won't come up isn't re-launched every poll. base=5s so it is never faster than the old
        // flat 5s wait. Reset when the device reaches Playing.
        private readonly BackoffPolicy reconnectBackoff = new BackoffPolicy(baseSeconds: 5.0, maxSeconds: 30.0);

        public DeviceCommunication(ICastHost applicationLogicIn, ILogger loggerIn)
        {
            applicationLogic = applicationLogicIn;
            logger = loggerIn;
            // No id copied in here: ChromeCastMessages reads CastReceiver.AppId when it builds the LAUNCH,
            // so a device discovered before the user pasted an id still launches the right receiver.
            chromeCastMessages = new ChromeCastMessages();
            chromeCastDestination = string.Empty;
            chromeCastSource = string.Format("client-8{0}", new Random().Next(10000, 99999));
            requestId = 0;
        }

        /// <summary>
        /// Launch the device, media is loaded when the device responded.
        /// </summary>
        public void LaunchAndLoadMedia()
        {
            if (device == null || IsDisposed)
                return;

            // Check to make sure the status of the device is received before streaming is started.
            if (device.GetDeviceState() == DeviceState.Undefined)
            {
                GetStatus();
                WaitDeviceStatusReceived(20);
            }

            pendingStatusMessage = false;
            device.SetDeviceState(DeviceState.LaunchingApplication, null);
            Connect();

            WaitDeviceConnected(Launch);
        }

        /// <summary>
        /// Wait till the status has been received.
        /// </summary>
        private bool WaitDeviceStatusReceived(int nrWaitMsec = 5)
        {
            var attempt = 0;
            while (pendingStatusMessage && attempt++ < nrWaitMsec)
            {
                Task.Delay(100).Wait();
            }

            if (pendingStatusMessage)
                return false;

            return true;
        }

        /// <summary>
        /// Wait till the connection is established.
        /// </summary>
        private void WaitDeviceConnected(Action callback, int nrWaitMsec = 5)
        {
            var attempt = 0;
            while (!isDeviceConnected() && attempt++ < nrWaitMsec)
            {
                Task.Delay(100).Wait();
            }

            if (isDeviceConnected())
                callback();
        }

        /// <summary>
        /// Send a connect message
        /// </summary>
        /// <param name="sourceId"></param>
        /// <param name="destinationId"></param>
        public void Connect(string? sourceId = null, string? destinationId = null)
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            SendMessage(chromeCastMessages.GetConnectMessage(sourceId, destinationId));
        }

        /// <summary>
        /// Send a launch message.
        /// </summary>
        public void Launch()
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            SendMessage(chromeCastMessages.GetLaunchMessage(GetNextRequestId()));
        }

        /// <summary>
        /// Send a load media message.
        /// </summary>
        public void LoadMedia()
        {
            if (applicationLogic == null || chromeCastMessages == null || device == null || IsDisposed)
                return;

            device.SetDeviceState(DeviceState.LoadingMedia, null);
            SendMessage(chromeCastMessages.GetLoadMessage(applicationLogic.GetStreamingUrl(), chromeCastSource, chromeCastDestination, GetNextRequestId(), applicationLogic.GetStreamMediaInfo()));
        }

        /// <summary>
        /// Send a pause media message.
        /// </summary>
        public void PauseMedia()
        {
            if (chromeCastMessages == null || device == null || IsDisposed)
                return;

            device.SetDeviceState(DeviceState.Paused, null);
            SendMessage(chromeCastMessages.GetPauseMessage(chromeCastApplicationSessionNr, chromeCastMediaSessionId, GetNextRequestId(), chromeCastSource, chromeCastDestination));
        }

        /// <summary>
        /// Send a play message.
        /// </summary>
        public void PlayMedia()
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            SendMessage(chromeCastMessages.GetPlayMessage(chromeCastApplicationSessionNr, chromeCastMediaSessionId, GetNextRequestId(), chromeCastSource, chromeCastDestination));
        }

        /// <summary>
        /// Set the volume to the new level.
        /// </summary>
        /// <param name="volumeSetting">the new volume level</param>
        public void VolumeSet(Volume volumeSetting)
        {
            if (volumeSetting == null || IsDisposed)
                return;

            nextVolumeSetItem = new VolumeSetItem { Setting = volumeSetting };
            SendVolumeSet();
        }

        /// <summary>
        /// Send a message to set the volume.
        /// </summary>
        private void SendVolumeSet()
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            if (!Connected)
                return;

            if ((nextVolumeSetItem != null && lastVolumeSetItem == null)
                || (lastVolumeSetItem != null && DateTime.Now.Subtract(lastVolumeSetItem.SendAt) > new TimeSpan(0, 0, 1)))
            {
                lastVolumeSetItem = nextVolumeSetItem!;
                lastVolumeSetItem.RequestId = GetNextRequestId();
                lastVolumeSetItem.SendAt = DateTime.Now;
                SendMessage(chromeCastMessages.GetVolumeSetMessage(lastVolumeSetItem.Setting, lastVolumeSetItem.RequestId));
                nextVolumeSetItem = null;
            }
        }

        /// <summary>
        /// Send a message to (un)mute the volume.
        /// </summary>
        /// <param name="muted">true = mute, false = unmute</param>
        public void VolumeMute(bool muted)
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            if (!Connected)
                return;

            SendMessage(chromeCastMessages.GetVolumeMuteMessage(muted, GetNextRequestId()));
        }

        /// <summary>
        /// Send a pong response to ping.
        /// </summary>
        public void Pong()
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            if (!Connected)
                return;

            SendMessage(chromeCastMessages.GetPongMessage());
        }

        /// <summary>
        /// Send a message to get the device media or receiver status.
        /// </summary>
        public void GetStatus()
        {
            if (chromeCastMessages == null || device == null || IsDisposed)
                return;

            var deviceState = device.GetDeviceState();
            if (!pendingStatusMessage)
            {
                pendingStatusMessage = true;
                if (deviceState == DeviceState.Playing ||
                    deviceState == DeviceState.Buffering ||
                    deviceState == DeviceState.Paused)
                {
                    SendMessage(chromeCastMessages.GetMediaStatusMessage(GetNextRequestId(), chromeCastSource, chromeCastDestination));
                }
                else
                {
                    GetReceiverStatus();
                }
            }
            else
            {
                logger.Log($"[{device.GetHost()}:{device.GetPort()}] Last received message: {lastReceivedMessage}");
                device.SetDeviceState(DeviceState.Undefined);
                device.CloseConnection();
                if (NoContactFor(15 * 60))
                    pendingStatusMessage = false;
            }

            deviceState = GiveUpOnAnApprovalNobodyAnswered(deviceState);

            // Keep trying to play when in playing mode. Which states deserve a nudge and which must be left
            // alone lives in ResumeDecision, where it can be stated as a fact and tested without a device.
            if (userMode == UserMode.Playing)
            {
                switch (ResumeDecision.ForState(deviceState))
                {
                    case ResumeAction.Reconnect:
                        ResumeAfterConnectionLoss();
                        break;
                    case ResumeAction.RelaunchAfterBackoff:
                        var deviceStateBefore = deviceState;
                        Task.Delay(reconnectBackoff.NextDelay()).Wait();
                        if (device.GetDeviceState() == deviceStateBefore)
                            ResumePlaying();
                        break;
                    case ResumeAction.None:
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// A prompt that appeared on a television nobody was watching would otherwise hold the card at
        /// "approve on device" until the app is restarted. After <see cref="LaunchApproval.ApprovalWindow"/>
        /// the wait ends and the device is treated as a cancelled launch, which lets the normal reconnect
        /// path try again.
        /// </summary>
        private DeviceState GiveUpOnAnApprovalNobodyAnswered(DeviceState deviceState)
        {
            if (deviceState != DeviceState.AwaitingUserApproval || awaitingApprovalSince == null)
                return deviceState;

            if (!LaunchApproval.HasWaitedTooLong(DateTime.Now - awaitingApprovalSince.Value))
                return deviceState;

            logger.Log($"[{device.GetHost()}:{device.GetPort()}] no answer to the launch prompt within {LaunchApproval.ApprovalWindow.TotalMinutes:0} minutes - giving up on it");
            awaitingApprovalSince = null;
            device.SetDeviceState(DeviceState.LoadCancelled, null);
            return DeviceState.LoadCancelled;
        }

        private bool NoContactFor(int nrSeconds)
        {
            return HadContact() && (DateTime.Now - lastReceivedMessage).Seconds > nrSeconds;
        }

        private bool HadContact()
        {
            return lastReceivedMessage != DateTime.MinValue;
        }

        /// <summary>
        /// Get the status text returned by the device.
        /// </summary>
        /// <returns>the status text</returns>
        public string GetStatusText()
        {
            return statusText!;
        }

        /// <summary>
        /// Send a message to get the receiver status.
        /// </summary>
        private void GetReceiverStatus()
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            ConnectionConnect();
            SendMessage(chromeCastMessages.GetReceiverStatusMessage(GetNextRequestId()));
        }

        /// <summary>
        /// Send a connect message, when not connected.
        /// </summary>
        private void ConnectionConnect()
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            if (!Connected)
            {
                SendMessage(chromeCastMessages.GetConnectMessage(null, null));
                WaitDeviceConnected(new Action(() => { Connected = true; }));
            }
        }

        /// <summary>
        /// Send a stop message.
        /// </summary>
        public void Stop(bool changeUserMode = false)
        {
            if (chromeCastMessages == null || device == null || IsDisposed)
                return;

            if (changeUserMode)
                userMode = UserMode.Stopped;

            var deviceState = device.GetDeviceState();

            // Hack to stop a device. If nothing is send the device is in buffering state and doesn't respond to stop messages. Send some silence first.
            if (deviceState == DeviceState.Buffering)
                device.SendSilence();

            SendMessage(chromeCastMessages.GetStopMessage(chromeCastApplicationSessionNr, chromeCastMediaSessionId, GetNextRequestId(), chromeCastSource, chromeCastDestination));

            // When the user actually stops (rather than the app pausing or re-connecting internally), close
            // the receiver application too. Without this the television keeps the Cast logo on screen for
            // minutes instead of returning to its menu or screensaver: the media STOP ends the stream, not
            // the receiver that is drawing that logo.
            if (changeUserMode && !string.IsNullOrEmpty(chromeCastApplicationSessionNr))
                SendMessage(chromeCastMessages.GetQuitApplicationMessage(chromeCastApplicationSessionNr, GetNextRequestId()));
        }

        /// <summary>
        /// Create a new request id (to be used in the messages to the device).
        /// </summary>
        /// <returns></returns>
        public int GetNextRequestId()
        {
            return ++requestId;
        }

        /// <summary>
        /// Do send a message.
        /// </summary>
        /// <param name="castMessage">the message to send</param>
        public void SendMessage(CastMessage castMessage)
        {
            if (chromeCastMessages == null || IsDisposed)
                return;

            if (NoContactFor(60))
            {
                device.CloseConnection();
                return;
            }

            var byteMessage = chromeCastMessages.MessageToByteArray(castMessage);
            sendMessage?.Invoke(byteMessage);

            // "out"/"in" stay English, like every other line we write. The log is a technical artefact
            // meant to be read by whoever is asked to diagnose it - a French user's log saying "entrée"
            // and "sortie" is harder to read and slips past the tooling that parses these lines.
            logger.Log($"out [{device.GetHost()}:{device.GetPort()}] [{device.GetDeviceState()}]: {castMessage.PayloadUtf8}");
        }

        /// <summary>
        /// Handle a message from the device.
        /// </summary>
        /// <param name="castMessage">the received message</param>
        public void OnReceiveMessage(CastMessage castMessage)
        {
            if (castMessage == null || device == null || IsDisposed)
                return;

            logger.Log($"in [{device.GetHost()}:{device.GetPort()}] [{device.GetDeviceState()}]: {castMessage.PayloadUtf8}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var message = JsonSerializer.Deserialize<PayloadMessageBase>(castMessage.PayloadUtf8, options)!;
            if (message.type != "PING" && message.type != "PONG")
            {
                lastReceivedMessage = DateTime.Now;
                pendingStatusMessage = false;
            }

            switch (message.@type)
            {
                case "RECEIVER_STATUS":
                    OnReceiveReceiverStatus(JsonSerializer.Deserialize<MessageReceiverStatus>(castMessage.PayloadUtf8, options));
                    break;
                case "MEDIA_STATUS":
                    OnReceiveMediaStatus(JsonSerializer.Deserialize<MessageMediaStatus>(castMessage.PayloadUtf8, options));
                    break;
                case "PING":
                    Pong();
                    break;
                case "PONG":
                    break;
                case "CLOSE":
                    OnReceiveCloseMessage();
                    break;
                case "LOAD_FAILED":
                    device.SetDeviceState(DeviceState.LoadFailed, null);
                    break;
                case "LOAD_CANCELLED":
                    device.SetDeviceState(DeviceState.LoadCancelled, null);
                    break;
                case "INVALID_REQUEST":
                    device.SetDeviceState(DeviceState.InvalidRequest, null);
                    break;
                case "LAUNCH_ERROR":
                    device.SetDeviceState(DeviceState.LoadCancelled, null);
                    break;
                case "LAUNCH_STATUS":
                    OnReceiveLaunchStatus(JsonSerializer.Deserialize<MessageLaunchStatus>(castMessage.PayloadUtf8, options));
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Tells the stage on the television what is playing now.
        /// <para>
        /// Only reaches KlangHub's own receiver: Google's default receiver knows nothing about this
        /// namespace and would drop the message, and there is no point building it for a device that
        /// cannot show it. Nothing here may throw or block - it runs on whatever thread noticed the
        /// track change, and a television that cannot be told is not a reason to stop the music.
        /// </para>
        /// </summary>
        public void SendStageUpdate(StageUpdate update)
        {
            if (update == null || !CanReachOurStage())
                return;

            try
            {
                SendMessage(chromeCastMessages.GetStageTrackMessage(update, chromeCastSource, chromeCastDestination));
            }
            catch (Exception ex)
            {
                logger.Log(ex, "DeviceCommunication.SendStageUpdate");
            }
        }

        /// <summary>Playing or held - the stage dims rather than clears when the music is paused.</summary>
        public void SendStageState(bool playing)
        {
            if (!CanReachOurStage())
                return;

            try
            {
                SendMessage(chromeCastMessages.GetStageStateMessage(playing, chromeCastSource, chromeCastDestination));
            }
            catch (Exception ex)
            {
                logger.Log(ex, "DeviceCommunication.SendStageState");
            }
        }

        /// <summary>
        /// True only when our own receiver is up and addressable. Both halves matter: an empty application
        /// id means the device is running Google's receiver, and an empty destination means no application
        /// has answered yet - either way the message would go nowhere.
        /// </summary>
        private bool CanReachOurStage() =>
            !IsDisposed
            && device != null
            && Connected
            && !string.IsNullOrWhiteSpace(CastReceiver.AppId)
            && !string.IsNullOrWhiteSpace(chromeCastDestination);

        /// <summary>
        /// Handle a LAUNCH_STATUS: the device is telling us the launch is waiting on a person, was allowed,
        /// or was refused. Without this, a device that asks for approval answers nothing we understand and
        /// KlangHub launches again every poll - which replaces the very prompt the listener is walking over
        /// to answer. Nine launches in five minutes, observed on the Enchant.
        /// </summary>
        private void OnReceiveLaunchStatus(MessageLaunchStatus? launchStatusMessage)
        {
            if (device == null || IsDisposed)
                return;

            var approval = LaunchApproval.Parse(launchStatusMessage?.status);
            var nextState = LaunchApproval.NextState(approval);
            if (nextState == null)
                return;

            awaitingApprovalSince = approval == LaunchApprovalStatus.Pending ? DateTime.Now : null;
            device.SetDeviceState(nextState.Value, null);
        }

        /// <summary>
        /// Handle a close message from the device.
        /// </summary>
        private void OnReceiveCloseMessage()
        {
            if (applicationLogic == null || device == null || IsDisposed)
                return;

            if (!(applicationLogic.GetAutoRestart()))
                userMode = UserMode.Stopped;

            var deviceState = device.GetDeviceState();
            if (deviceState == DeviceState.Playing ||
                deviceState == DeviceState.Buffering ||
                deviceState == DeviceState.Paused ||
                deviceState == DeviceState.LoadingMedia ||
                deviceState == DeviceState.LoadingMediaCheckFirewall)
            {
                Stop();
            }
            device.SetDeviceState(DeviceState.Closed, null);
            Connected = false;
            var cancellationTokeSource = new CancellationTokenSource();
            applicationLogic.StartTask(() => {
                for (int i = 0; i < 20; i++)
                {
                    Task.Delay(100).Wait();

                    if (cancellationTokeSource.IsCancellationRequested)
                        return;
                }

                GetReceiverStatus();
            }, cancellationTokeSource);
        }

        /// <summary>
        /// Try to resume playing.
        /// </summary>
        /// <summary>
        /// Rebuild the session because the connection was lost, not because the user asked for anything.
        ///
        /// The loss can be noticed by the audio socket failing, by the status poll finding the device in an
        /// error state, or by a CLOSE message arriving - whichever comes first should act, and the rest
        /// should stand down. A direct <see cref="ResumePlaying"/> from the user is never held back here.
        /// </summary>
        public void ResumeAfterConnectionLoss()
        {
            if (device == null || IsDisposed || userMode != UserMode.Playing)
                return;

            if (!reconnectGate.TryEnter())
                return;

            ResumePlaying();
        }

        public void ResumePlaying()
        {
            if (device == null || IsDisposed)
                return;

            logger.Log($"[{device.GetHost()}:{device.GetPort()}] ResumePlaying");
            userMode = UserMode.Playing;
            pendingStatusMessage = false;

            var cancellationTokenSource = new CancellationTokenSource();
            applicationLogic.StartTask(() =>
            {
                try
                {
                Task.Delay(2000).Wait();
                var deviceState = device.GetDeviceState();
                if (deviceState == DeviceState.Playing ||
                    deviceState == DeviceState.Buffering ||
                    deviceState == DeviceState.Paused ||
                    deviceState == DeviceState.LoadingMedia ||
                    deviceState == DeviceState.LoadingMediaCheckFirewall)
                {
                    Stop();
                }

                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                if (deviceState != DeviceState.ConnectError)
                    device.SetDeviceState(DeviceState.NotConnected, null);
                Disconnect();
                Task.Delay(2000).Wait();

                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                if (device.GetDeviceState() == DeviceState.NotConnected
                        || device.GetDeviceState() == DeviceState.Connected
                        || device.GetDeviceState() == DeviceState.Closed
                        || device.GetDeviceState() == DeviceState.Idle)
                {
                    device.OnGetStatus();
                    if (device.IsStatusTextBlank())
                        WaitDeviceConnected(PlayStop, 50);
                }
                }
                finally
                {
                    // However this attempt ended - connected, cancelled, or turned away by a state guard -
                    // the next reason to reconnect may act immediately. Leaving the gate to time out made
                    // the fast-recovery paths wait out the remainder of a rebuild that was already over.
                    reconnectGate.Leave();
                }
            }, cancellationTokenSource);
        }

        /// <summary>
        /// Handle a media status message from the device.
        /// </summary>
        /// <param name="mediaStatusMessage">the media status message</param>
        private void OnReceiveMediaStatus(MessageMediaStatus? mediaStatusMessage)
        {
            if (mediaStatusMessage == null || device == null || IsDisposed)
                return;

            Connected = true;

            if (mediaStatusMessage?.status?.First()?.volume?.controlType != null && 
                mediaStatusMessage?.status?.First()?.volume?.stepInterval > 0)
                device.OnVolumeUpdate(mediaStatusMessage.status.First().volume);

            chromeCastMediaSessionId = mediaStatusMessage!.status.Any() ? mediaStatusMessage.status.First().mediaSessionId : 1;

            if (device.IsConnected() && mediaStatusMessage.status.Any())
            {
                switch (mediaStatusMessage.status.First().playerState)
                {
                    case "IDLE":
                        device.SetDeviceState(DeviceState.Idle, null);
                        break;
                    case "BUFFERING":
                        device.SetDeviceState(DeviceState.Buffering, GetPlayingTime(mediaStatusMessage));
                        break;
                    case "PAUSED":
                        device.SetDeviceState(DeviceState.Paused, null);
                        break;
                    case "PLAYING":
                        device.SetDeviceState(DeviceState.Playing, GetPlayingTime(mediaStatusMessage));
                        reconnectBackoff.Reset(); // recovered — next stall starts from the base wait again
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Extract the playing time from a media status message.
        /// </summary>
        /// <param name="mediaStatusMessage">a media status message</param>
        /// <returns>the playing time, format hh:mm</returns>
        private string? GetPlayingTime(MessageMediaStatus mediaStatusMessage)
        {
            if (mediaStatusMessage == null || IsDisposed)
                return string.Empty;

            if (mediaStatusMessage.status != null && mediaStatusMessage.status.First() != null)
            {
                var minutes = ((int)(mediaStatusMessage.status.First().currentTime) % 3600) / 60;
                var hours = ((int)mediaStatusMessage.status.First().currentTime) / 3600;
                return $"{hours}:{minutes:D2}";
            }

            return null;
        }

        /// <summary>
        /// Handle a receiver status message from the device.
        /// </summary>
        /// <param name="receiverStatusMessage">a receiver status message</param>
        private void OnReceiveReceiverStatus(MessageReceiverStatus? receiverStatusMessage)
        {
            if (receiverStatusMessage == null || device == null || IsDisposed)
                return;

            if (receiverStatusMessage?.status?.volume != null)
                device.OnVolumeUpdate(receiverStatusMessage.status.volume);

            statusText = receiverStatusMessage?.status?.applications?.FirstOrDefault()?.statusText;
            statusText = statusText?.Replace("Default Media Receiver", string.Empty);
            var state = device.GetDeviceState();
            if (state == DeviceState.ConnectError || state == DeviceState.NotConnected || state == DeviceState.Closed)
            {
                device.SetDeviceState(DeviceState.Connected, null);
                Connected = true;
            }
            device.SetDeviceState(device.GetDeviceState(), $" {statusText}");

            if (receiverStatusMessage != null && receiverStatusMessage.status != null && receiverStatusMessage.status.applications != null)
            {
                // Not a hard-coded default-receiver id: the LAUNCH already honours whatever id the user
                // pasted, so matching the answer against Google's would throw away the reply that carries
                // the transport id and session - our receiver would start and never be given anything to play.
                var deviceApplication = receiverStatusMessage.status.applications.Where(a => CastReceiver.IsOurApplication(a.appId));
                if (deviceApplication.Any())
                {
                    chromeCastDestination = deviceApplication.First().transportId;
                    chromeCastApplicationSessionNr = deviceApplication.First().sessionId;

                    if (device.GetDeviceState().Equals(DeviceState.LaunchingApplication))
                    {
                        device.SetDeviceState(DeviceState.LaunchedApplication, null);
                        Connect(chromeCastSource, chromeCastDestination);
                        LoadMedia();
                    }
                }
            }

            if (lastVolumeSetItem != null && lastVolumeSetItem.RequestId == receiverStatusMessage!.requestId)
            {
                lastVolumeSetItem = null;
                SendVolumeSet();
            }
        }

        /// <summary>
        /// Set the callbacks for the device communication.
        /// </summary>
        public void SetCallback(IDevice deviceIn, Action<byte[]> sendMessageIn, Func<bool> isDeviceConnectedIn)
        {
            device = deviceIn;
            sendMessage = sendMessageIn;
            isDeviceConnected = isDeviceConnectedIn;
            pendingStatusMessage = false;
        }

        /// <summary>
        /// Handle a clcik on the play button.
        /// </summary>
        public void OnPlayStop_Click()
        {
            if (userMode == UserMode.Stopped)
                userMode = UserMode.Playing;
            else
                userMode = UserMode.Stopped;

            PlayStop();
        }

        /// <summary>
        /// Play or stop.
        /// </summary>
        private void PlayStop()
        {
            switch (device.GetDeviceState())
            {
                case DeviceState.Buffering:
                case DeviceState.Playing:
                    Stop();
                    break;
                case DeviceState.LaunchingApplication:
                case DeviceState.LaunchedApplication:
                case DeviceState.LoadingMedia:
                case DeviceState.LoadingMediaCheckFirewall:
                case DeviceState.Idle:
                    LoadMedia();
                    break;
                case DeviceState.Paused:
                    PlayMedia();
                    break;
                case DeviceState.NotConnected:
                case DeviceState.Connected:
                case DeviceState.ConnectError:
                case DeviceState.Closed:
                case DeviceState.LoadCancelled:
                case DeviceState.LoadFailed:
                case DeviceState.InvalidRequest:
                case DeviceState.Undefined:
                    LaunchAndLoadMedia();
                    break;
                case DeviceState.Disposed:
                    break;
                default:
                    break;
            }
        }

        public void Disconnect()
        {
            Connected = false;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
            userMode = UserMode.Stopped;
        }

        /// <summary>
        /// Return the usermode.
        /// </summary>
        /// <returns>the usermode</returns>
        public UserMode GetUserMode()
        {
            return userMode;
        }
    }
}
