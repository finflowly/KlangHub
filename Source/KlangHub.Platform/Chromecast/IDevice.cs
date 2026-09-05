using System;
using System.Net.Sockets;
using KlangHub.Communication;
using KlangHub.Communication.Classes;
using KlangHub.ProtocolBuffer;
using KlangHub.Classes;
using KlangHub.Discover;
using System.Threading;

namespace KlangHub.Application
{
    public interface IDevice
    {
        void SetDeviceState(DeviceState disposed, string? text = null);
        void Initialize(DiscoveredDevice discoveredDevice, Action<DeviceEureka> deviceInformationCallback, Action<IDevice> stopGroup, Action<Action, CancellationTokenSource?> startTaskIn, Func<IDevice, bool> isGroupStatusBlankIn, Action<bool> autoMuteIn);
        bool AddStreamingConnection(string remoteAddress, Socket socket, SupportedStreamFormat streamFormat);
        void OnGetStatus();

        /// <summary>Tell this device's stage what is playing. A device without our own receiver ignores it.</summary>
        void SendStageUpdate(KlangHub.Core.NowPlaying.StageUpdate update);

        /// <summary>Tell this device's stage whether the music is running or held.</summary>
        void SendStageState(bool playing);
        void OnRecordingDataAvailable(byte[] dataToSend, AudioFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat);
        void OnClickPlayPause(object sender, EventArgs e);
        string GetUsn();
        string GetHost();
        string GetFriendlyName();
        DeviceState GetDeviceState();
        void VolumeUp();
        void VolumeDown();
        void VolumeMute();
        void VolumeSet(float level);
        void Stop(bool changeUserMode);
        void Start();
        void OnReceiveMessage(CastMessage castMessage);
        int GetPort();
        DiscoveredDevice GetDiscoveredDevice();
        void SendSilence();
        bool IsGroup();
        bool IsConnected();
        void OnVolumeUpdate(Volume volume);
        void ResumePlaying();

        /// <summary>Rebuild the session after the connection to this device was lost. Does nothing unless
        /// the user actually wants playback, and lets only one rebuild run at a time - a loss can be noticed
        /// from several places at once.</summary>
        void ResumeAfterConnectionLoss();
        DeviceEureka GetEureka();
        void StartTask(Action action);
        void Dispose();
        bool IsStatusTextBlank();
        string GetStatusText();
        bool IsStatusTextBlankCheck(string statusText);
        UserMode GetUserMode();
        int GetVolumeLevel();
        bool IsDisposed();
        void OnClickPlayStop();
        void CloseConnection();
    }
}