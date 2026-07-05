using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// The header's master-volume fader (replaces the old Up/Down/Mute buttons, per the approved UI concept):
    /// a compact rounded pill with a mute icon, a track + thumb, and a live percentage. Dragging sets every
    /// visible device card to that absolute level in one motion (each card still enforces its own per-speaker
    /// maximum-volume hard cap). The mute icon replays the existing all-devices mute toggle unchanged.
    /// </summary>
    public sealed class MasterVolumeControl : Control
    {
        private float level = 0.5f;
        private bool dragging;
        private Rectangle muteRect, trackRect;

        public event Action<float>? VolumeDragged;
        public event Action? MuteClicked;

        public MasterVolumeControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(176, 36);
            BackColor = Theme.Ink;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Ink);

            var pill = new RectangleF(0, 1, Width - 1, Height - 2);
            Theme.FillRounded(g, pill, Height / 2f, Theme.Surface);
            Theme.DrawRounded(g, pill, Height / 2f, Theme.Line);

            muteRect = new Rectangle(10, (Height - 20) / 2, 20, 20);
            DrawSpeaker(g, muteRect);

            int trackX = muteRect.Right + 10;
            int pctW = 34;
            trackRect = new Rectangle(trackX, Height / 2 - 3, Width - trackX - pctW - 12, 6);
            DrawTrack(g, trackRect);

            string pct = (int)Math.Round(level * 100) + "%";
            using var b = new SolidBrush(Theme.Ivory);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            g.DrawString(pct, Theme.Small, b, new RectangleF(Width - pctW - 8, 0, pctW, Height), sf);
        }

        private void DrawSpeaker(Graphics g, Rectangle r)
        {
            using var pen = new Pen(Theme.Slate, 1.5f) { LineJoin = LineJoin.Round };
            int x = r.X, cy = r.Y + r.Height / 2;
            var body = new[] { new PointF(x, cy - 3), new PointF(x + 4, cy - 3), new PointF(x + 8, cy - 7), new PointF(x + 8, cy + 7), new PointF(x + 4, cy + 3), new PointF(x, cy + 3) };
            g.DrawPolygon(pen, body);
            g.DrawArc(pen, x + 7, cy - 5, 7, 10, -55, 110);
        }

        private void DrawTrack(Graphics g, Rectangle r)
        {
            var track = new RectangleF(r.X, r.Y, r.Width, r.Height);
            Theme.FillRounded(g, track, r.Height / 2f, Theme.Ink2);
            Theme.DrawRounded(g, track, r.Height / 2f, Theme.Line);
            float fillW = r.Width * level;
            if (fillW > 3)
            {
                var fr = new RectangleF(r.X, r.Y, fillW, r.Height);
                using var lg = new LinearGradientBrush(fr, Theme.AmberDim, Theme.Amber, LinearGradientMode.Horizontal);
                using var fp = Theme.RoundedRect(fr, r.Height / 2f);
                g.FillPath(lg, fp);
            }
            float tx = r.X + fillW, ty = r.Y + r.Height / 2f;
            using var tb = new SolidBrush(Theme.Ivory);
            g.FillEllipse(tb, tx - 6, ty - 6, 12, 12);
            using var tp = new Pen(Color.FromArgb(80, Theme.Amber), 3f);
            g.DrawEllipse(tp, tx - 6, ty - 6, 12, 12);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (muteRect.Contains(e.Location)) { MuteClicked?.Invoke(); return; }
            if (e.Y >= trackRect.Y - 8 && e.Y <= trackRect.Bottom + 8) { dragging = true; ApplyFromX(e.X); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) ApplyFromX(e.X);
            else Cursor = (muteRect.Contains(e.Location) || (e.Y >= trackRect.Y - 8 && e.Y <= trackRect.Bottom + 8)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e) => dragging = false;

        private void ApplyFromX(int x)
        {
            if (trackRect.Width <= 0) return;
            float t = Math.Clamp((x - trackRect.X) / (float)trackRect.Width, 0f, 1f);
            if (Math.Abs(t - level) < 0.004f) return;
            level = t;
            Invalidate();
            VolumeDragged?.Invoke(level);
        }
    }
}
