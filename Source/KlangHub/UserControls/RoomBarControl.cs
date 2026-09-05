using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// The header of one room in the grouped view: its glyph and name, what is playing in it, and a fader
    /// that moves the whole room at once.
    /// <para>
    /// The fader is <b>proportional</b>. A room rarely wants one level - the TV sits at 13 %, the soundbar at
    /// 11 %, the speaker at 20 % - so the fader scales all of them by the same FACTOR: those three become
    /// 26/22/40 at double, not one flat number. The mix the user dialled in survives; only its loudness moves.
    /// Each card still enforces its own hard cap, so a room at 100 % cannot blow the flat apart - the speaker
    /// capped at 23 % stops at 23 %.
    /// </para>
    /// </summary>
    public sealed class RoomBarControl : Control
    {
        private const int BarH = 54;

        private readonly Func<IReadOnlyList<DeviceControl>> members;
        private Rectangle iconRect, muteRect, minusRect, plusRect, trackRect;
        private bool dragging;
        private int hovered = -1;        // 0 = mute, 1 = minus, 2 = plus

        /// <summary>Raised after this bar changed volumes, so the page can refresh its summary.</summary>
        public event EventHandler? RoomChanged;

        public string RoomName { get; }

        public RoomBarControl(string roomName, Func<IReadOnlyList<DeviceControl>> membersIn)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.Selectable, true);
            RoomName = roomName;
            members = membersIn;
            Height = BarH;
            Margin = new Padding(7, 10, 7, 2);
            BackColor = Theme.Ink;
            TabStop = true;
        }

        private IReadOnlyList<DeviceControl> Members => members();

        /// <summary>The room's level: the average of its speakers, which is what the fader shows and what a
        /// drag moves away from.</summary>
        public int Level
        {
            get => RoomVolume.LevelOf(Members.Select(d => d.VolumePercent).ToArray());
        }

        private bool AllMuted => Members.Count > 0 && Members.All(d => d.IsMuted);

        // ---------------- painting ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Theme.PaintBackdrop(g, this, blurDivisor: 5, veilScale: 0.85f);

            var body = new RectangleF(2, 2, Width - 5, Height - 5);
            Theme.FillGlass(g, body, Theme.RadControl + 2, Theme.Surface,
                            Color.FromArgb(120, Theme.LineHi), Theme.GlassAlpha - 26);

            var list = Members;
            bool playing = list.Any(d => d.IsPlaying);
            var accent = playing ? Theme.Amber : Theme.Slate;

            int padL = 16, padR = Width - 16;

            // ---- glyph + name ----
            iconRect = new Rectangle(padL, (Height - 24) / 2, 24, 24);
            RoomPresets.DrawIcon(g, iconRect, RoomName, accent);

            int textX = iconRect.Right + 12;
            using (var b = new SolidBrush(Theme.Ivory))
                g.DrawString(RoomName, Theme.Name, b, textX, 8);

            string sub = Summary(list, playing);
            using (var b = new SolidBrush(playing ? Theme.Amber : Theme.Slate))
                g.DrawString(sub, Theme.Small, b, textX + 1, 30);

            // ---- controls, right to left ----
            int cy = Height / 2;
            int pctW = 42;
            var pct = AllMuted ? Properties.Strings.Card_Muted_Text : Level + "%";
            using (var b = new SolidBrush(AllMuted ? Theme.Amber : Theme.Slate))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                g.DrawString(pct, Theme.Data, b, new RectangleF(padR - pctW, cy - 10, pctW, 20), sf);

            plusRect = new Rectangle(padR - pctW - 30, cy - 12, 24, 24);
            minusRect = new Rectangle(plusRect.Left - 28, cy - 12, 24, 24);
            DrawStep(g, minusRect, false, hovered == 1);
            DrawStep(g, plusRect, true, hovered == 2);

            int trackRight = minusRect.Left - 12;
            int trackLeft = Math.Max(textX + 190, trackRight - 200);
            trackRect = new Rectangle(trackLeft, cy - 5, Math.Max(60, trackRight - trackLeft), 10);
            Theme.DrawAmberSlider(g, trackRect, Level / 100f, 12f);

            muteRect = new Rectangle(trackLeft - 34, cy - 11, 22, 22);
            Theme.DrawSpeakerGlyph(g, muteRect, AllMuted ? Theme.Amber : (hovered == 0 ? Theme.Ivory : Theme.Slate), AllMuted);

            if (Focused) Theme.DrawFocusRing(g, body, Theme.RadControl + 2);
        }

        private static string Summary(IReadOnlyList<DeviceControl> list, bool playing)
        {
            // speakers, not rooms - a room bar counts what stands IN the room
            string count = list.Count == 1
                ? Properties.Strings.Room_SpeakersOne_Text
                : string.Format(Properties.Strings.Room_SpeakersMany_Text, list.Count);

            int n = list.Count(d => d.IsPlaying);
            if (!playing || n == 0)
                return count;

            string p = n == 1
                ? Properties.Strings.Label_RoomSummaryPlayingOne_Text
                : string.Format(Properties.Strings.Label_RoomSummaryPlayingMany_Text, n);
            return count + " · " + p;
        }

        private static void DrawStep(Graphics g, Rectangle r, bool plus, bool hover)
        {
            var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            Theme.FillRounded(g, rf, 7f, hover ? Theme.Raised : Theme.Ink2);
            Theme.DrawRounded(g, rf, 7f, hover ? Theme.LineHi : Theme.Line);
            using var pen = new Pen(Theme.Ivory, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
            if (plus) g.DrawLine(pen, cx, cy - 5, cx, cy + 5);
        }

        // ---------------- interaction ----------------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) { ApplyFromX(e.X); return; }

            int h = muteRect.Contains(e.Location) ? 0
                  : minusRect.Contains(e.Location) ? 1
                  : plusRect.Contains(e.Location) ? 2 : -1;
            bool onTrack = TrackHit(e.Location);
            Cursor = (h >= 0 || onTrack) ? Cursors.Hand : Cursors.Default;
            if (h != hovered) { hovered = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hovered != -1) { hovered = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button != MouseButtons.Left) return;

            if (muteRect.Contains(e.Location)) { ToggleMute(); return; }
            if (minusRect.Contains(e.Location)) { Shift(-1); return; }
            if (plusRect.Contains(e.Location)) { Shift(+1); return; }

            // The X test is not optional. Without it the accepted band was the full width of the bar at
            // that height, so clicking the read-only percent figure on the right landed past the track's
            // end, clamped to 1 and drove every uncapped speaker in the room to 100 % - from a text label,
            // in one click. OnMouseMove already tested both axes; OnMouseDown did not.
            if (TrackHit(e.Location))
            {
                dragging = true;
                ApplyFromX(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) => dragging = false;

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down: Shift(-1); e.Handled = true; break;
                case Keys.Right:
                case Keys.Up: Shift(+1); e.Handled = true; break;
                case Keys.M: ToggleMute(); e.Handled = true; break;
            }
            base.OnKeyDown(e);
        }

        private void ToggleMute()
        {
            bool mute = !AllMuted;
            foreach (var d in Members) d.SetMuted(mute);
            Invalidate();
            RoomChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Takes the room to <paramref name="target"/> percent by scaling every speaker with the same factor,
        /// which is what keeps 13/11/20 reading as 13/11/20 one octave up rather than as three identical
        /// numbers. Two cases cannot be scaled and are handled explicitly: a room that is silent has no ratio
        /// to preserve (everyone goes to the target), and a speaker at 0 would stay at 0 forever, so it is
        /// lifted along with the room.
        /// </summary>
        /// <summary>
        /// The mix as it was before the room was pulled to silence, so it can be restored on the way back up.
        ///
        /// Arithmetic alone cannot do this: at zero there are no ratios left, so a room dragged to 0 and
        /// back came up flat - 13/11/20 became 15/15/15 and the balance the owner had set was gone for good.
        /// The bar remembers it instead, which is where that knowledge belongs; RoomVolume stays a pure
        /// function of what it is handed.
        /// </summary>
        private int[]? mixBeforeSilence;

        public void SetLevel(int target)
        {
            var list = Members;
            if (list.Count == 0) return;

            var current = list.Select(d => d.VolumePercent).ToArray();
            var caps = list.Select(d => d.MaxVolumePercent).ToArray();

            // Coming back up from a silenced room: scale the remembered mix, not the row of zeroes.
            var basis = current;
            if (current.All(v => v <= 0) && target > 0
                && mixBeforeSilence != null && mixBeforeSilence.Length == current.Length)
                basis = mixBeforeSilence;
            else if (current.Any(v => v > 0))
                mixBeforeSilence = current;

            var scaled = RoomVolume.Scale(basis, caps, target);

            for (int i = 0; i < list.Count; i++)
                list[i].SetVolumePercent(scaled[i]);

            Invalidate();
            RoomChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Trims the room by a couple of points - the same proportional move, just smaller.</summary>
        /// <summary>
        /// One press of + or -, in the step the room can actually feel.
        ///
        /// Every Cast device reports the step it works in and they differ (1 % on the television, 2 % on a
        /// Google Home, 4 % on the Enchant), so a room takes the COARSEST of its members: with anything
        /// finer, a press would be swallowed by the device with the largest step and only some of the room
        /// would move. The level is proportional, so the step scales the whole room in ratio as before.
        /// </summary>
        private int StepPercent
        {
            get
            {
                int step = 0;
                foreach (var member in Members)
                    step = Math.Max(step, member.StepPercent);
                return Math.Max(1, step);
            }
        }

        /// <summary>The one place that decides whether a point is on the room's fader. Both axes, always.</summary>
        private bool TrackHit(Point p) => IsOnTrack(trackRect, p);

        /// <summary>
        /// Pure geometry, so the rule can be tested without a window: a point counts as on the fader only
        /// when it is within reach on BOTH axes. Testing the height alone accepted the whole width of the
        /// bar, which turned the read-only percent figure into a click-to-maximum control.
        /// </summary>
        internal static bool IsOnTrack(Rectangle track, Point p)
            => p.Y >= track.Y - 8 && p.Y <= track.Bottom + 8
               && p.X >= track.X - 4 && p.X <= track.Right + 4;

        private void Shift(int direction) => SetLevel(Level + direction * StepPercent);

        private void ApplyFromX(int x)
        {
            if (trackRect.Width <= 0) return;
            float t = Math.Clamp((x - trackRect.X) / (float)trackRect.Width, 0f, 1f);
            SetLevel((int)Math.Round(t * 100));
        }
    }
}
