using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Controls playback on a single endpoint. Deliberately provider-neutral: no protobuf, TLS,
    /// NAudio or UI types. A Chromecast implementation wraps the existing DeviceCommunication state
    /// machine unchanged and maps <c>DeviceState</c> onto <see cref="PlaybackState"/> at the boundary.
    ///
    /// Commands are fire-and-forget; observed results arrive via <see cref="StateChanged"/> /
    /// <see cref="VolumeChanged"/>. (Cloud/REST providers may later want Task-returning variants.)
    /// </summary>
    public interface IPlaybackSession : IDisposable
    {
        /// <summary>The endpoint this session controls.</summary>
        CastDeviceDescriptor Device { get; }

        /// <summary>Current neutral playback state.</summary>
        PlaybackState State { get; }

        /// <summary>
        /// Human-readable status/diagnostic detail for the current state (may be empty). Preserves
        /// actionable hints the enum can't carry, e.g. the Chromecast "check your firewall" message.
        /// </summary>
        string StatusText { get; }

        /// <summary>Current volume level and mute.</summary>
        VolumeStatus Volume { get; }

        /// <summary>Raised when <see cref="State"/> (or <see cref="StatusText"/>) changes.</summary>
        event EventHandler<PlaybackState> StateChanged;

        /// <summary>Raised when the endpoint reports a new volume (level and/or mute).</summary>
        event EventHandler<VolumeStatus> VolumeChanged;

        /// <summary>Open the session. Idempotent / re-entrant: safe to call to re-establish a dropped connection.</summary>
        void Connect();

        /// <summary>Start or resume playback of the source on this endpoint.</summary>
        void Play();

        void Pause();
        void Stop();

        /// <summary>Set the endpoint volume level (0.0 .. 1.0).</summary>
        void SetVolume(float level);

        void SetMuted(bool muted);

        /// <summary>Ask the endpoint for a fresh status update (result arrives via events).</summary>
        void RequestStatus();

        void Disconnect();
    }
}
