using KlangHub.Communication;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// While the user wants to hear something, the status timer keeps nudging every device back towards
    /// playing. Which nudge a device gets depends on where it stands - and one state must get no nudge at
    /// all: a device that is waiting for somebody to approve the launch on its own screen. Asking again
    /// does not make the person any faster, it only replaces the question they were about to answer.
    /// </summary>
    public class ResumeDecisionTests
    {
        [Theory]
        [InlineData(DeviceState.NotConnected)]
        [InlineData(DeviceState.Disposed)]
        [InlineData(DeviceState.ConnectError)]
        [InlineData(DeviceState.LoadFailed)]
        [InlineData(DeviceState.LoadCancelled)]
        [InlineData(DeviceState.InvalidRequest)]
        [InlineData(DeviceState.Closed)]
        [InlineData(DeviceState.Connected)]
        public void Reconnects_when_the_connection_is_the_thing_that_is_missing(DeviceState state)
        {
            Assert.Equal(ResumeAction.Reconnect, ResumeDecision.ForState(state));
        }

        [Theory]
        [InlineData(DeviceState.LaunchingApplication)]
        [InlineData(DeviceState.LaunchedApplication)]
        [InlineData(DeviceState.Idle)]
        public void Tries_playing_again_when_the_device_is_connected_but_silent(DeviceState state)
        {
            Assert.Equal(ResumeAction.RelaunchAfterBackoff, ResumeDecision.ForState(state));
        }

        [Theory]
        [InlineData(DeviceState.LoadingMedia)]
        [InlineData(DeviceState.LoadingMediaCheckFirewall)]
        [InlineData(DeviceState.Buffering)]
        [InlineData(DeviceState.Playing)]
        [InlineData(DeviceState.Paused)]
        [InlineData(DeviceState.Undefined)]
        public void Leaves_a_device_alone_while_it_is_getting_on_with_it(DeviceState state)
        {
            Assert.Equal(ResumeAction.None, ResumeDecision.ForState(state));
        }

        /// <summary>
        /// The Enchant on 2026-09-05: nine LAUNCH requests over five minutes, none of them answered, because
        /// the device was waiting for a person - not for another request. This is the assertion that stops
        /// that from happening again.
        /// </summary>
        [Fact]
        public void Sends_no_second_launch_while_the_device_waits_for_approval()
        {
            Assert.Equal(ResumeAction.None, ResumeDecision.ForState(DeviceState.AwaitingUserApproval));
        }
    }
}
