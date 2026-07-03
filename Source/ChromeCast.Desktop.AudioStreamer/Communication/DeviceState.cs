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
        LoadingMediaCheckFirewall
    };
}
