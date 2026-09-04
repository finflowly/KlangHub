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
            bool onTrack = e.Y >= trackRect.Y - 8 && e.Y <= trackRect.Bottom + 8
                           && e.X >= trackRect.X - 4 && e.X <= trackRect.Right + 4;
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
            if (minusRect.Contains(e.Location)) { Shift(-2); return; }
            if (plusRect.Contains(e.Location)) { Shift(+2); return; }

            if (e.Y >= trackRect.Y - 8 && e.Y <= trackRect.Bottom + 8)
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
                case Keys.Down: Shift(-2); e.Handled = true; break;
                case Keys.Right:
                case Keys.Up: Shift(+2); e.Handled = true; break;
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
        public void SetLevel(int target)
        {
            var list = Members;
            if (list.Count == 0) return;

            var scaled = RoomVolume.Scale(
                list.Select(d => d.VolumePercent).ToArray(),
                list.Select(d => d.MaxVolumePercent).ToArray(),
                target);

            for (int i = 0; i < list.Count; i++)
                list[i].SetVolumePercent(scaled[i]);

            Invalidate();
            RoomChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Trims the room by a couple of points - the same proportional move, just smaller.</summary>
        private void Shift(int delta) => SetLevel(Level + delta);

        private void ApplyFromX(int x)
        {
            if (trackRect.Width <= 0) return;
            float t = Math.Clamp((x - trackRect.X) / (float)trackRect.Width, 0f, 1f);
            SetLevel((int)Math.Round(t * 100));
        }
    }
}
