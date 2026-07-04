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
