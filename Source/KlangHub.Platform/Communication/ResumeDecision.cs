namespace KlangHub.Communication
{
    /// <summary>What the status timer should do with a device that is not playing but should be.</summary>
    public enum ResumeAction
    {
        /// <summary>Nothing. Either the device is already on its way, or nudging it would do harm.</summary>
        None,

        /// <summary>The connection is what is missing - build it again from the start.</summary>
        Reconnect,

        /// <summary>Connected but silent: wait out the backoff, then ask it to play again.</summary>
        RelaunchAfterBackoff
    }

    /// <summary>
    /// The rule the status timer follows while the user wants to hear something. Kept apart from
    /// <see cref="DeviceCommunication"/> so the one case that matters most can be stated as a fact rather
    /// than inferred from a running device: a device awaiting approval is left alone.
    /// </summary>
    public static class ResumeDecision
    {
        public static ResumeAction ForState(DeviceState state)
        {
            switch (state)
            {
                case DeviceState.NotConnected:
                case DeviceState.Disposed:
                case DeviceState.ConnectError:
                case DeviceState.LoadFailed:
                case DeviceState.LoadCancelled:
                case DeviceState.InvalidRequest:
                case DeviceState.Closed:
                case DeviceState.Connected:
                    return ResumeAction.Reconnect;

                case DeviceState.LaunchingApplication:
                case DeviceState.LaunchedApplication:
                case DeviceState.Idle:
                    return ResumeAction.RelaunchAfterBackoff;

                // AwaitingUserApproval belongs here and nowhere else. A second LAUNCH does not reach the
                // person standing in front of the television any faster; it replaces the prompt they were
                // about to answer. Observed on the Enchant on 2026-09-05: nine launches, five minutes.
                case DeviceState.AwaitingUserApproval:
                case DeviceState.LoadingMedia:
                case DeviceState.LoadingMediaCheckFirewall:
                case DeviceState.Buffering:
                case DeviceState.Playing:
                case DeviceState.Paused:
                default:
                    return ResumeAction.None;
            }
        }
    }
}
