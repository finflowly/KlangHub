namespace KlangHub.Core.Models
{
    /// <summary>
    /// States for the socket connection used for control messages.
    /// </summary>
    public enum DeviceConnectionState
    {
        None,
        Connecting,
        Connected,
        Error,
        Disconnected
    }
}
