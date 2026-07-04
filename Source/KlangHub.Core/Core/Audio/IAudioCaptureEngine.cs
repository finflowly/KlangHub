using System;
using System.Collections.Generic;

namespace KlangHub.Core.Audio
{
    /// <summary>Payload for <see cref="IAudioCaptureEngine.DevicesChanged"/>.</summary>
    public sealed class AudioDeviceListEventArgs : EventArgs
    {
        public IReadOnlyList<AudioCaptureDevice> Devices { get; }
        public AudioCaptureDevice DefaultDevice { get; }

        public AudioDeviceListEventArgs(IReadOnlyList<AudioCaptureDevice> devices, AudioCaptureDevice defaultDevice)
        {
            Devices = devices;
            DefaultDevice = defaultDevice;
        }
    }

    /// <summary>
    /// Captures desktop/microphone audio and emits it as neutral <see cref="AudioFrame"/>s.
    /// Deliberately free of any UI (WinForms / IMainForm) and NAudio types so the contract lives in Core.
    ///
    /// Data-flow model: the UI configures the engine via <see cref="Start"/>/<see cref="Apply"/> (settings in)
    /// and observes it via events (data/level/device-list out). The engine never calls back into the UI.
    /// </summary>
    public interface IAudioCaptureEngine : IDisposable
    {
        /// <summary>A chunk of captured audio (or generated silence) is ready for the streaming pipeline.</summary>
        event EventHandler<AudioFrame> DataAvailable;

        /// <summary>A raw sample buffer for a VU / level meter (was <c>IMainForm.ShowWavMeterValue</c>).</summary>
        event EventHandler<byte[]> LevelSampled;

        /// <summary>The set of available capture endpoints changed (was <c>IMainForm.AddRecordingDevices</c>).</summary>
        event EventHandler<AudioDeviceListEventArgs> DevicesChanged;

        /// <summary>Enumerate the currently available capture/render endpoints.</summary>
        IReadOnlyList<AudioCaptureDevice> GetDevices();

        /// <summary>The current system default render endpoint, or null if none.</summary>
        AudioCaptureDevice GetDefaultDevice();

        /// <summary>Begin periodic device scanning and start capturing with the given settings.</summary>
        void Start(AudioCaptureSettings settings);

        /// <summary>
        /// Switch capture to the given settings (device / format / stereo change). If the requested
        /// device cannot be started, the engine falls back to the first working endpoint.
        /// </summary>
        /// <returns>true if capture started on some device; otherwise false.</returns>
        bool Apply(AudioCaptureSettings settings);

        /// <summary>Stop capturing but keep the engine alive.</summary>
        void Stop();

        /// <summary>Stop and restart capture using the most recently applied settings.</summary>
        void Restart();
    }
}
