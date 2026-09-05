using System.Drawing;
using KlangHub.UserControls;
using Xunit;

namespace KlangHub.Tests.UserControls
{
    /// <summary>
    /// The room fader accepted a click by its height alone, which made the whole width of the bar behave
    /// like the fader - including the read-only percent figure on the right, whose x lies past the end of
    /// the track and therefore clamped to 100 %. One click on a text label, every uncapped speaker in the
    /// room at full volume. These tests pin the rule on both axes.
    /// </summary>
    public class RoomBarTrackHitTests
    {
        // The geometry a 750-px window produces: BarH 54, so the track sits at y 22 with height 10.
        private static readonly Rectangle Track = new(242, 22, 380, 10);

        [Fact]
        public void The_track_itself_is_hit()
        {
            Assert.True(RoomBarControl.IsOnTrack(Track, new Point(400, 27)));
            Assert.True(RoomBarControl.IsOnTrack(Track, new Point(Track.X, Track.Y)));
            Assert.True(RoomBarControl.IsOnTrack(Track, new Point(Track.Right, Track.Bottom)));
        }

        [Fact]
        public void A_small_margin_around_it_is_forgiving_but_finite()
        {
            Assert.True(RoomBarControl.IsOnTrack(Track, new Point(Track.X - 4, Track.Y - 8)));
            Assert.True(RoomBarControl.IsOnTrack(Track, new Point(Track.Right + 4, Track.Bottom + 8)));
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(Track.X - 5, 27)));
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(Track.Right + 5, 27)));
        }

        [Fact]
        public void The_percent_readout_right_of_the_track_is_not_a_volume_control()
        {
            // Drawn right-aligned near the bar's right edge - the same height as the track, far past its end.
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(700, 27)));
        }

        [Fact]
        public void The_room_name_and_subtitle_left_of_the_track_are_not_a_volume_control()
        {
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(53, 27)));
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(120, 30)));
        }

        [Fact]
        public void A_point_far_above_or_below_is_not_a_hit()
        {
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(400, 5)));
            Assert.False(RoomBarControl.IsOnTrack(Track, new Point(400, 50)));
        }
    }
}
