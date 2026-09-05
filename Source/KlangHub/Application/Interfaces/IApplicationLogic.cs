using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using KlangHub.Classes;
using KlangHub.Discover;
using KlangHub.Streaming;
using NAudio.Wave;

namespace KlangHub.Application.Interfaces
{
    public interface IApplicationLogic : ICastHost
    {
        void Initialize();

        /// <summary>Tells the user, once per run, that the close button left the app in the tray.</summary>
        void NotifyMinimizedToTray();
        void SetLagThreshold(int lagThreshold);
        void OnRecordingDataAvailable(AudioFrame frame);
        void OnStreamingRequestConnect(Socket handlerSocket, string httpRequest);
        void SetDependencies(IMainForm mainForm);
        void CloseApplication();
        void OnSetAutoRestart(bool autoRestart);
        void ChangeIPAddressUsed(IPAddress ipAddress);
        void ScanForDevices();
        void ResetSettings();
        void SetStreamFormat(SupportedStreamFormat format);
        void SetCulture(string culture);
        void SetStreamTitle(string title);

        /// <summary>
        /// Re-read the metadata settings and start the now-playing sources over with them. Called when the
        /// user changes a switch or the file path, so a correction takes effect while the music is running
        /// rather than at the next start.
        /// </summary>
        void ApplyNowPlayingOptions();
        bool WasPlaying(DiscoveredDevice discoveredDevice);
        void ClearMp3Buffer();
        void SaveSettings();
        void SetRecordingDevice(AudioCaptureDevice? recordingDevice);
    }
}