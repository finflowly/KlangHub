using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// Two faults the review found in the room fader's arithmetic: a mid-range request could put a speaker
    /// at full volume, and turning a room DOWN could switch a silenced speaker on.
    /// </summary>
    public class RoomVolumeCeilingTests
    {
        private static readonly int[] NoCaps = { 100, 100, 100 };

        [Fact]
        public void No_speaker_is_asked_for_more_than_the_scale_has()
        {
            // Two already-loud speakers, room at 85, user asks for 100. Unlimited the factor is 1.176, which
            // wants 94 and 106 - the second clamps to 100 and the mix is gone. Limited by what the loudest
            // can still take, both move by 1.111 and the ratio survives.
            var scaled = RoomVolume.Scale(new[] { 80, 90 }, new[] { 100, 100 }, 100);

            Assert.Equal(89, scaled[0]);
            Assert.Equal(100, scaled[1]);
            Assert.True(scaled[0] < 94, "the quieter speaker overshot, so a clamp had happened");
        }

        [Fact]
        public void The_mix_is_kept_rather_than_flattened_against_the_ceiling()
        {
            // The ratio 80:90 is 0.889. After scaling it must still be, within rounding.
            var scaled = RoomVolume.Scale(new[] { 80, 90 }, new[] { 100, 100 }, 100);

            double before = 80 / 90.0;
            double after = scaled[0] / (double)scaled[1];
            Assert.True(System.Math.Abs(before - after) < 0.02, $"ratio drifted from {before:F3} to {after:F3}");
        }

        [Fact]
        public void The_loudest_speaker_may_still_reach_the_end_of_the_scale()
        {
            // Honest about the limit of this protection: a speaker at 80 in a room being turned up goes
            // towards 100, and 100 is where the scale ends. What stops it earlier is a per-speaker cap -
            // that is what the cap is for. The factor limit only prevents the OVERSHOOT that used to
            // destroy the mix on the way there.
            var scaled = RoomVolume.Scale(new[] { 5, 5, 80 }, NoCaps, 40);

            Assert.Equal(100, scaled[2]);
            Assert.Equal(scaled[0], scaled[1]);
        }

        [Fact]
        public void A_cap_is_what_actually_holds_a_loud_speaker_back()
        {
            var scaled = RoomVolume.Scale(new[] { 5, 5, 80 }, new[] { 100, 100, 85 }, 40);

            Assert.Equal(85, scaled[2]);
        }

        [Fact]
        public void Turning_a_room_down_does_not_switch_on_a_silenced_speaker()
        {
            // One speaker at 20, one deliberately silenced. Level 10; the user presses "−".
            int[] volumes = { 20, 0 };
            int[] caps = { 100, 100 };

            var quieter = RoomVolume.Scale(volumes, caps, 6);

            Assert.Equal(0, quieter[1]);
            Assert.True(quieter[0] < 20, "the playing speaker should have come down");
        }

        [Fact]
        public void Turning_a_room_up_still_brings_a_silent_speaker_along()
        {
            int[] volumes = { 20, 0 };
            int[] caps = { 100, 100 };

            var louder = RoomVolume.Scale(volumes, caps, 30);

            Assert.Equal(30, louder[1]);
        }

        [Fact]
        public void A_capped_speaker_still_stops_without_holding_the_room_back()
        {
            // The cap is the user's decision about ONE speaker - it must not brake the others.
            var loud = RoomVolume.Scale(new[] { 20, 20 }, new[] { 100, 23 }, 100);

            Assert.Equal(100, loud[0]);
            Assert.Equal(23, loud[1]);
        }
    }
}
