using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Core
{
    /// <summary>
    /// The three decisions behind every volume change, pulled out of the device so they can be tested.
    /// <para>
    /// They were the whole of the SET_VOLUME loop seen on two devices, which reported <c>0.13999999</c>
    /// and <c>0.20000005</c> back and were sent another SET_VOLUME for their trouble, endlessly. None of
    /// those digits came from the devices: the level was built by adding a step to itself in a loop until
    /// it passed the target, so seven additions of 0.02 produced exactly that. It was then compared with
    /// the clean target using <c>!=</c> on a float, which can only disagree.
    /// </para>
    /// <para>
    /// The tolerance that did exist was <c>stepInterval</c> - and a device whose volume cannot be changed
    /// reports a step of 0, so the tolerance was 0 for precisely the devices that could never agree.
    /// </para>
    /// </summary>
    public class VolumeLevelTests
    {
        [Theory]
        [InlineData("fixed")]
        [InlineData("FIXED")]
        [InlineData("Fixed")]
        public void A_device_whose_volume_is_fixed_is_recognised(string controlType)
            => Assert.True(VolumeLevel.IsFixed(controlType));

        [Theory]
        [InlineData("attenuation")]
        [InlineData("master")]
        [InlineData("")]
        [InlineData(null)]
        public void Any_other_control_type_is_not(string? controlType)
            => Assert.False(VolumeLevel.IsFixed(controlType));

        [Fact]
        public void Two_levels_a_rounding_error_apart_are_the_same_level()
        {
            // The two values actually reported from hardware, against the targets that produced them.
            Assert.True(VolumeLevel.Same(0.13999999f, 0.14f));
            Assert.True(VolumeLevel.Same(0.20000005f, 0.20f));
        }

        [Fact]
        public void Two_levels_a_step_apart_are_not()
        {
            Assert.False(VolumeLevel.Same(0.14f, 0.16f));
            Assert.False(VolumeLevel.Same(0f, 1f));
        }

        [Fact]
        public void A_level_is_snapped_to_the_nearest_step_the_device_accepts()
        {
            // Rounded in one go, not reached by adding the step over and over - which is where the
            // accumulated error came from in the first place.
            Assert.Equal(0.14f, VolumeLevel.Quantise(0.138f, 0.02f), 5);
            Assert.Equal(0.20f, VolumeLevel.Quantise(0.21f, 0.05f), 5);
            Assert.Equal(0.50f, VolumeLevel.Quantise(0.5f, 0.05f), 5);
        }

        [Fact]
        public void A_device_that_reports_no_step_gets_the_level_unchanged()
        {
            // stepInterval 0 is what a fixed-volume device reports. Dividing by it would be a loop that
            // never ends - the shape of the bug this replaces.
            Assert.Equal(0.37f, VolumeLevel.Quantise(0.37f, 0f), 5);
            Assert.Equal(0.37f, VolumeLevel.Quantise(0.37f, -1f), 5);
        }

        [Theory]
        [InlineData(1.5f, 1f)]
        [InlineData(-0.2f, 0f)]
        [InlineData(float.NaN, 0f)]
        public void A_level_outside_the_range_is_brought_back_into_it(float given, float expected)
            => Assert.Equal(expected, VolumeLevel.Quantise(given, 0.05f), 5);

        // ---- the per-speaker hard cap, which is the other half of the loop ----

        [Fact]
        public void A_level_above_the_cap_is_pushed_back_down()
        {
            Assert.True(VolumeLevel.ShouldPushDownToCap(reportedPercent: 80, capPercent: 60, isFixed: false, attemptsSoFar: 0));
        }

        [Fact]
        public void A_level_at_or_below_the_cap_is_left_alone()
        {
            Assert.False(VolumeLevel.ShouldPushDownToCap(60, 60, false, 0));
            Assert.False(VolumeLevel.ShouldPushDownToCap(30, 60, false, 0));
        }

        [Fact]
        public void A_device_that_cannot_change_its_volume_is_not_pushed_at_all()
        {
            // This was the loop as the user saw it: every status message from a fixed-volume speaker
            // above the cap produced another SET_VOLUME that could not possibly take.
            Assert.False(VolumeLevel.ShouldPushDownToCap(80, 60, isFixed: true, attemptsSoFar: 0));
        }

        [Fact]
        public void A_device_that_will_not_come_down_is_stopped_being_asked()
        {
            // Some devices clamp to their own maximum and simply will not go where they are told. Three
            // tries is generous; after that, asking again forever helps nobody.
            Assert.True(VolumeLevel.ShouldPushDownToCap(80, 60, false, attemptsSoFar: 2));
            Assert.False(VolumeLevel.ShouldPushDownToCap(80, 60, false, attemptsSoFar: 3));
            Assert.False(VolumeLevel.ShouldPushDownToCap(80, 60, false, attemptsSoFar: 99));
        }

        [Fact]
        public void Snapping_never_leaves_the_range_either()
        {
            // 0.99 with a step of 0.05 rounds to 1.00, not past it.
            Assert.Equal(1f, VolumeLevel.Quantise(0.99f, 0.05f), 5);
            Assert.Equal(0f, VolumeLevel.Quantise(0.01f, 0.05f), 5);
        }
    }
}
