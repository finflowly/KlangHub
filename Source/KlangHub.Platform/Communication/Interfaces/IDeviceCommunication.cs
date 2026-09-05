using System;
using KlangHub.Application;
using KlangHub.Communication.Classes;
using KlangHub.ProtocolBuffer;

namespace KlangHub.Communication.Interfaces
{
    public interface IDeviceCommunication
    {
        void Connect(string? sourceId = null, string? destinationId = null);
        void Launch();
        void LaunchAndLoadMedia();
        void LoadMedia();
        void PauseMedia();
        void Pong();
        void GetStatus();
        string GetStatusText();
        void OnReceiveMessage(CastMessage castMessage);
        void VolumeSet(Volume volumeSetting);
        void VolumeMute(bool muted);
        void SetCallback(IDevice device, Action<byte[]> sendMessage, Func<bool> isDeviceConnected);
        void Disconnect();
        void Stop(bool changeUserMode = false);
        void OnPlayStop_Click();
        void ResumePlaying();

        /// <summary>Rebuild the session after the connection was lost; only one rebuild at a time.</summary>
        void ResumeAfterConnectionLoss();
        void Dispose();
        UserMode GetUserMode();
    }
}