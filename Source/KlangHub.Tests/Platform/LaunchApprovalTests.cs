using System;
using KlangHub.Communication;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// A Cast device may hold a LAUNCH back until somebody approves it on the device itself, and says so
    /// with a LAUNCH_STATUS message. Observed on the Enchant on 2026-09-05: the launch sat unanswered for
    /// five minutes, then {"launchRequestId":25,"status":"USER_ALLOWED","type":"LAUNCH_STATUS"} arrived and
    /// playback started at once. The status strings are Chromium's own
    /// (components/media_router/common/providers/cast/channel/cast_message_util.h).
    /// </summary>
    public class LaunchApprovalTests
    {
        [Theory]
        [InlineData("USER_PENDING_AUTHORIZATION", LaunchApprovalStatus.Pending)]
        [InlineData("USER_ALLOWED", LaunchApprovalStatus.Allowed)]
        [InlineData("USER_NOT_ALLOWED", LaunchApprovalStatus.NotAllowed)]
        public void Reads_the_statuses_a_cast_device_sends(string wire, LaunchApprovalStatus expected)
        {
            Assert.Equal(expected, LaunchApproval.Parse(wire));
        }

        // Anything we have not seen must leave the existing flow alone rather than invent a decision:
        // only USER_PENDING_AUTHORIZATION and USER_NOT_ALLOWED are allowed to stop a launch.
        [Theory]
        [InlineData("NOTIFICATION_DISABLED")]
        [InlineData("something_google_added_later")]
        [InlineData("")]
        [InlineData(null)]
        public void Treats_an_unfamiliar_status_as_unknown(string? wire)
        {
            Assert.Equal(LaunchApprovalStatus.Unknown, LaunchApproval.Parse(wire));
        }

        [Fact]
        public void Reads_the_status_regardless_of_casing()
        {
            Assert.Equal(LaunchApprovalStatus.Allowed, LaunchApproval.Parse("user_allowed"));
        }

        [Fact]
        public void A_pending_approval_parks_the_device_in_its_own_state()
        {
            Assert.Equal(DeviceState.AwaitingUserApproval, LaunchApproval.NextState(LaunchApprovalStatus.Pending));
        }

        [Fact]
        public void An_allowed_launch_goes_back_to_launching()
        {
            // Not straight to LaunchedApplication: the RECEIVER_STATUS that follows is what proves the
            // application is really up, and it is that message which starts the media load.
            Assert.Equal(DeviceState.LaunchingApplication, LaunchApproval.NextState(LaunchApprovalStatus.Allowed));
        }

        [Fact]
        public void A_refused_launch_ends_like_any_other_cancellation()
        {
            Assert.Equal(DeviceState.LoadCancelled, LaunchApproval.NextState(LaunchApprovalStatus.NotAllowed));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(60)]
        [InlineData(299)]
        public void Waits_while_the_dialog_could_still_be_answered(int secondsWaited)
        {
            Assert.False(LaunchApproval.HasWaitedTooLong(TimeSpan.FromSeconds(secondsWaited)));
        }

        [Theory]
        [InlineData(300)]
        [InlineData(3600)]
        public void Gives_up_on_a_dialog_nobody_answered(int secondsWaited)
        {
            // Without an end to the wait, a prompt that appeared on a television nobody was watching would
            // keep the card in "approve on device" until the app is restarted.
            Assert.True(LaunchApproval.HasWaitedTooLong(TimeSpan.FromSeconds(secondsWaited)));
        }

        [Fact]
        public void An_unknown_status_changes_nothing()
        {
            // The launch flow carries on exactly as it did before this message type was understood.
            Assert.Null(LaunchApproval.NextState(LaunchApprovalStatus.Unknown));
        }
    }
}
