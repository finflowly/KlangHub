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

        /// <summary>
        /// The device is holding the launch until somebody allows it there. Neither an error nor merely
        /// connecting: the listener has to walk over to the device, and only a state of its own can say so.
        /// </summary>
        AwaitingApproval,

        Error
    }
}
