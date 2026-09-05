namespace KlangHub.Communication
{
    public enum DeviceState
    {
        Undefined,
        NotConnected,
        Idle,
        Disposed,
        LaunchingApplication,
        LaunchedApplication,
        LoadingMedia,
        Buffering,
        Playing,
        Paused,
        ConnectError,
        LoadFailed,
        LoadCancelled,
        InvalidRequest,
        Closed,
        Connected,
        LoadingMediaCheckFirewall,

        /// <summary>
        /// The device is healthy and answering, but it is holding the launch until somebody allows it
        /// on the device itself. Deliberately not an error state: nothing is broken, and nothing the
        /// sender does will speed it up.
        /// </summary>
        AwaitingUserApproval
    };
}
