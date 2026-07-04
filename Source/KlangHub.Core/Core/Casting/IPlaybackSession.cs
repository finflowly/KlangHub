using System;
using System.Threading.Tasks;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Controls playback on a single endpoint. Deliberately provider-neutral: no protobuf, TLS,
    /// NAudio or UI types. A Chromecast implementation wraps the existing DeviceCommunication state
    /// machine unchanged and maps <c>DeviceState</c> onto <see cref="PlaybackState"/> at the boundary.
    ///
    /// Control commands are Task-returning (2.2b-M2): network providers (AirPlay/Snapcast) can await
    /// success/failure; the Chromecast impl does its synchronous work and returns a completed Task.
    /// Observed state still arrives via <see cref="StateChanged"/> / <see cref="VolumeChanged"/>.
    ///
    /// <see cref="IDisposable.Dispose"/> releases any PER-SESSION resources. A provider that hosts the
    /// session on a shared, longer-lived object (Chromecast: the session IS the live device) implements
    /// Dispose() as a no-op - disposing a session must never tear down a shared endpoint.
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
        Task Connect();

        /// <summary>Start or resume playback of the source on this endpoint.</summary>
        Task Play();

        Task Pause();
        Task Stop();

        /// <summary>
        /// Toggle playback as a single user action (the tray/UI play-stop button). Provider-specific:
        /// each provider decides, from its own state, whether this starts, resumes, (re)loads or stops.
        /// Chromecast maps it 1:1 onto its existing OnClickPlayStop state machine, so no neutral consumer
        /// has to reconstruct that logic from the coarser <see cref="PlaybackState"/>.
        /// </summary>
        Task TogglePlayStop();

        /// <summary>Set the endpoint volume level (0.0 .. 1.0).</summary>
        Task SetVolume(float level);

        Task SetMuted(bool muted);

        /// <summary>Ask the endpoint for a fresh status update (result arrives via events).</summary>
        Task RequestStatus();

        Task Disconnect();
    }
}
