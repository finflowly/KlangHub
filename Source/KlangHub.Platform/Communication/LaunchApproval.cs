using System;

namespace KlangHub.Communication
{
    /// <summary>What a device answered when asked to launch a receiver application.</summary>
    public enum LaunchApprovalStatus
    {
        /// <summary>No answer we recognise. The launch flow carries on exactly as it did before.</summary>
        Unknown,

        /// <summary>Somebody has to allow this at the device. Asking again does not help.</summary>
        Pending,

        /// <summary>Allowed - the launch may proceed.</summary>
        Allowed,

        /// <summary>Refused at the device. Retrying only repeats the question.</summary>
        NotAllowed
    }

    /// <summary>
    /// A Cast device may hold a LAUNCH back until somebody approves it on the device itself, and reports that
    /// with a LAUNCH_STATUS message rather than by answering the launch. Reading it is what keeps KlangHub
    /// from firing a fresh LAUNCH every fifteen seconds into a device that is merely waiting for a person.
    ///
    /// The status strings are Chromium's own (components/media_router/common/providers/cast/channel/
    /// cast_message_util.h), which is the only written-down list of them there is.
    /// </summary>
    public static class LaunchApproval
    {
        public static LaunchApprovalStatus Parse(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return LaunchApprovalStatus.Unknown;

            return status.Trim().ToUpperInvariant() switch
            {
                "USER_PENDING_AUTHORIZATION" => LaunchApprovalStatus.Pending,
                "USER_ALLOWED" => LaunchApprovalStatus.Allowed,
                "USER_NOT_ALLOWED" => LaunchApprovalStatus.NotAllowed,
                _ => LaunchApprovalStatus.Unknown
            };
        }

        /// <summary>
        /// The device state a LAUNCH_STATUS puts the device into, or null when the message says nothing we
        /// understand and the launch should carry on untouched.
        /// </summary>
        public static DeviceState? NextState(LaunchApprovalStatus status) => status switch
        {
            LaunchApprovalStatus.Pending => DeviceState.AwaitingUserApproval,
            // Back to launching rather than launched: what proves the receiver is really up is the
            // RECEIVER_STATUS that follows, and that message is what triggers the media load.
            LaunchApprovalStatus.Allowed => DeviceState.LaunchingApplication,
            LaunchApprovalStatus.NotAllowed => DeviceState.LoadCancelled,
            _ => null
        };

        /// <summary>
        /// How long a pending approval is left alone. Long enough to walk to the television and answer the
        /// prompt; short enough that a prompt nobody saw does not hold the card for the rest of the evening.
        /// </summary>
        public static readonly TimeSpan ApprovalWindow = TimeSpan.FromMinutes(5);

        /// <summary>True once a pending approval has gone unanswered for <see cref="ApprovalWindow"/>.</summary>
        public static bool HasWaitedTooLong(TimeSpan waited) => waited >= ApprovalWindow;
    }
}
