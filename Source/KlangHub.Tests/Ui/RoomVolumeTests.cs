using System;
using System.Linq;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// The room fader's arithmetic. Tested here rather than on real speakers: this code decides how loud a
    /// living room gets, and a wrong factor is something you hear before you see it.
    /// </summary>
    public class RoomVolumeTests
    {
        private static readonly int[] NoCaps = { 100, 100, 100 };

        [Fact]
        public void Scales_the_mix_instead_of_flattening_it()
        {
            // the user's own example: TV 13, soundbar 11, speaker 20 - the room reads 15
            int[] volumes = { 13, 11, 20 };
            Assert.Equal(15, RoomVolume.LevelOf(volumes));

            var doubled = RoomVolume.Scale(volumes, NoCaps, 30);

            // every speaker doubled; the ratios between them survive
            Assert.Equal(new[] { 26, 22, 40 }, doubled);
            Assert.Equal(29, RoomVolume.LevelOf(doubled));   // rounding keeps it within a point of the target
        }

        [Fact]
        public void Turning_a_room_down_keeps_the_same_shape()
        {
            int[] volumes = { 40, 20, 10 };
            var quieter = RoomVolume.Scale(volumes, NoCaps, 12);   // room was ~23, so roughly half

            Assert.True(quieter[0] > quieter[1] && quieter[1] > quieter[2]);
            Assert.All(quieter, v => Assert.InRange(v, 0, 40));
        }

        [Fact]
        public void A_capped_speaker_stops_at_its_cap()
        {
            // the bathroom speaker is capped at 23 %: a room pushed to 100 % must not get past it
            int[] volumes = { 20, 20 };
            int[] caps = { 100, 23 };

            var loud = RoomVolume.Scale(volumes, caps, 100);

            Assert.Equal(100, loud[0]);
            Assert.Equal(23, loud[1]);
        }

        [Fact]
        public void A_silent_room_simply_moves_to_the_target()
        {
            // nothing to scale from - without this the room could never come back up
            var lifted = RoomVolume.Scale(new[] { 0, 0, 0 }, NoCaps, 18);
            Assert.Equal(new[] { 18, 18, 18 }, lifted);
        }

        [Fact]
        public void A_single_silent_speaker_joins_the_room_rather_than_staying_at_zero()
        {
            var raised = RoomVolume.Scale(new[] { 20, 0 }, new[] { 100, 100 }, 30);
            Assert.Equal(60, raised[0]);   // 20 scaled by the room's factor (room was 10, target 30)
            Assert.Equal(30, raised[1]);   // was silent, joins at the room's level
        }

        [Fact]
        public void Zero_means_zero_for_everyone()
        {
            var silent = RoomVolume.Scale(new[] { 40, 12, 7 }, NoCaps, 0);
            Assert.All(silent, v => Assert.Equal(0, v));
        }

        [Fact]
        public void Never_leaves_the_valid_range()
        {
            var high = RoomVolume.Scale(new[] { 90, 95 }, new[] { 100, 100 }, 100);
            Assert.All(high, v => Assert.InRange(v, 0, 100));
        }

        [Fact]
        public void An_empty_room_is_a_no_op()
        {
            Assert.Empty(RoomVolume.Scale(Array.Empty<int>(), Array.Empty<int>(), 50));
            Assert.Equal(0, RoomVolume.LevelOf(Array.Empty<int>()));
        }

        [Fact]
        public void Refuses_a_cap_list_that_does_not_match()
        {
            Assert.Throws<ArgumentException>(() => RoomVolume.Scale(new[] { 10, 20 }, new[] { 100 }, 50));
        }
    }
}
