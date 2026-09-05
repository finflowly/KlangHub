using KlangHub.Communication;

namespace KlangHub.Platform.Casting.Chromecast
{
    /// <summary>
    /// Maps the Chromecast-specific <see cref="DeviceState"/> (17 values) onto the neutral
    /// <see cref="PlaybackState"/> (10 values). The one lossy case, LoadingMediaCheckFirewall, maps
    /// to Loading; the actionable "check firewall" hint is preserved separately via the session's
    /// StatusText (not by inventing a Chromecast-specific neutral state).
    /// </summary>
    internal static class ChromecastStateMapper
    {
        public static PlaybackState ToPlaybackState(DeviceState state) => state switch
        {
            DeviceState.Undefined => PlaybackState.Unknown,
            DeviceState.NotConnected => PlaybackState.Disconnected,
            DeviceState.Closed => PlaybackState.Disconnected,
            DeviceState.Disposed => PlaybackState.Disconnected,
            DeviceState.Connected => PlaybackState.Connected,
            DeviceState.Idle => PlaybackState.Idle,
            DeviceState.LoadCancelled => PlaybackState.Idle,          // a cancellation, not a failure
            DeviceState.LaunchingApplication => PlaybackState.Connecting,
            DeviceState.LaunchedApplication => PlaybackState.Connecting,
            // Held at the device until a person allows it. Mapping it to Error or Unknown is what told
            // the user the device was unreachable while it was answering every status request it got - but
            // Connecting is not right either: after five silent minutes of "connecting" the app looks hung
            // when in truth it is waiting for the listener to walk over and press allow. Its own state is
            // the only thing that can say that on the tile.
            DeviceState.AwaitingUserApproval => PlaybackState.AwaitingApproval,
            DeviceState.LoadingMedia => PlaybackState.Loading,
            DeviceState.LoadingMediaCheckFirewall => PlaybackState.Loading,
            DeviceState.Buffering => PlaybackState.Buffering,
            DeviceState.Playing => PlaybackState.Playing,
            DeviceState.Paused => PlaybackState.Paused,
            DeviceState.ConnectError => PlaybackState.Error,
            DeviceState.LoadFailed => PlaybackState.Error,
            DeviceState.InvalidRequest => PlaybackState.Error,
            _ => PlaybackState.Unknown
        };
    }
}
