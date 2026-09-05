using KlangHub.Communication;
using KlangHub.Core.Casting;
using KlangHub.Platform.Casting.Chromecast;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ChromecastStateMapperTests
    {
        [Theory]
        [InlineData(DeviceState.Undefined, PlaybackState.Unknown)]
        [InlineData(DeviceState.NotConnected, PlaybackState.Disconnected)]
        [InlineData(DeviceState.Closed, PlaybackState.Disconnected)]
        [InlineData(DeviceState.Disposed, PlaybackState.Disconnected)]
        [InlineData(DeviceState.Connected, PlaybackState.Connected)]
        [InlineData(DeviceState.Idle, PlaybackState.Idle)]
        [InlineData(DeviceState.LoadCancelled, PlaybackState.Idle)]
        [InlineData(DeviceState.LaunchingApplication, PlaybackState.Connecting)]
        [InlineData(DeviceState.LaunchedApplication, PlaybackState.Connecting)]
        // Waiting for someone to allow the launch is neither the "Unknown" that made the tile claim the
        // device was unreachable, nor the "Connecting" that hides the one thing the listener must be told:
        // the device is waiting for them.
        [InlineData(DeviceState.AwaitingUserApproval, PlaybackState.AwaitingApproval)]
        [InlineData(DeviceState.LoadingMedia, PlaybackState.Loading)]
        [InlineData(DeviceState.LoadingMediaCheckFirewall, PlaybackState.Loading)]
        [InlineData(DeviceState.Buffering, PlaybackState.Buffering)]
        [InlineData(DeviceState.Playing, PlaybackState.Playing)]
        [InlineData(DeviceState.Paused, PlaybackState.Paused)]
        [InlineData(DeviceState.ConnectError, PlaybackState.Error)]
        [InlineData(DeviceState.LoadFailed, PlaybackState.Error)]
        [InlineData(DeviceState.InvalidRequest, PlaybackState.Error)]
        public void Maps_each_device_state_to_the_expected_neutral_state(DeviceState state, PlaybackState expected)
        {
            Assert.Equal(expected, ChromecastStateMapper.ToPlaybackState(state));
        }
    }
}
