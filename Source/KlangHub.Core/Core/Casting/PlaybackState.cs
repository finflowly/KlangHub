namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Provider-neutral playback state. Supersedes the Chromecast-specific <c>DeviceState</c>;
    /// each provider maps its own states onto this at the Platform boundary.
    /// </summary>
    public enum PlaybackState
    {
        Unknown,
        Disconnected,
        Connecting,
        Connected,
        Idle,
        Loading,
        Buffering,
        Playing,
        Paused,
        Error
    }
}
