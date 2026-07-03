using System;
using System.Net.Sockets;
using System.Windows.Forms;
using NAudio.Wave;
using KlangHub.UserControls;
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
        void SetDeviceState(DeviceState disposed, string text = null);
        void Initialize(DiscoveredDevice discoveredDevice, Action<DeviceEureka> deviceInformationCallback, Action<IDevice> stopGroup, Action<Action, CancellationTokenSource> startTaskIn, Func<IDevice, bool> isGroupStatusBlankIn, Action<bool> autoMuteIn);
        bool AddStreamingConnection(string remoteAddress, Socket socket);
        void OnGetStatus();
        void OnRecordingDataAvailable(byte[] dataToSend, WaveFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat);
        void OnClickPlayPause(object sender, EventArgs e);
        string GetUsn();
        string GetHost();
        string GetFriendlyName();
        DeviceState GetDeviceState();
        void SetDeviceControl(DeviceControl deviceControl);
        ToolStripMenuItem GetMenuItem();
        void SetMenuItem(ToolStripMenuItem menuItem);
        void VolumeUp();
        void VolumeDown();
        void VolumeMute();
        void VolumeSet(float level);
        void Stop(bool changeUserMode);
        void Start();
        void OnReceiveMessage(CastMessage castMessage);
        DeviceControl GetDeviceControl();
        int GetPort();
        DiscoveredDevice GetDiscoveredDevice();
        void SendSilence();
        bool IsGroup();
        bool IsConnected();
        void OnVolumeUpdate(Volume volume);
        void ResumePlaying();
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