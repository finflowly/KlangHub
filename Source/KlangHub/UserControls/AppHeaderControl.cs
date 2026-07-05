using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// The branded app header: the amber ring wordmark, the slogan, and a live device summary. Docked to the
    /// top of the devices tab. The summary is fed by a counter delegate polled once a second so "N Geräte ·
    /// M spielen" stays current as devices appear and playback starts/stops.
    /// </summary>
    public sealed class AppHeaderControl : UserControl
    {
        private readonly Func<(int total, int playing)> counter;
        private readonly System.Windows.Forms.Timer timer;
        private int total, playing;

        // The brand slogan, redefined brand-true (the artwork's rings fill every room). Kept subtle.
        private const string Slogan = "Ein Klang für jeden Raum";

        public AppHeaderControl(Func<(int total, int playing)> counterIn)
        {
            counter = counterIn;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 64;
            BackColor = Theme.Ink;

            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => Refresh();
            timer.Start();
            Disposed += (s, e) => timer.Dispose();
        }

        public new void Refresh()
        {
            try { (total, playing) = counter(); } catch { /* counter is best-effort */ }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Ink);

            // bottom divider
            using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            // wordmark ring logo
            int cx = 34, cy = Height / 2;
            using (var p3 = new Pen(Color.FromArgb(70, Theme.Amber), 1.4f)) g.DrawEllipse(p3, cx - 15, cy - 15, 30, 30);
            using (var p2 = new Pen(Color.FromArgb(140, Theme.Amber), 1.4f)) g.DrawEllipse(p2, cx - 9, cy - 9, 18, 18);
            using (var b = new SolidBrush(Theme.Amber)) g.FillEllipse(b, cx - 4, cy - 4, 8, 8);
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(cx - 10, cy - 10, 20, 20);
                using var pb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(90, Theme.Amber), SurroundColors = new[] { Color.FromArgb(0, Theme.Amber) } };
                g.FillPath(pb, glow);
            }

            int tx = 60;
            using (var b = new SolidBrush(Theme.Ivory)) g.DrawString("KlangHub", Theme.Title, b, tx, 11);
            using (var b = new SolidBrush(Theme.Slate)) g.DrawString(Slogan, Theme.Small, b, tx + 1, 36);

            // live summary (right)
            string count = total == 1 ? "1 Gerät" : $"{total} Geräte";
            var szc = g.MeasureString(count, Theme.Body);
            string plays = playing > 0 ? $"{playing} spielen" : "bereit";
            var szp = g.MeasureString(plays, Theme.Small);
            float rx = Width - 20;
            using (var b = new SolidBrush(playing > 0 ? Theme.Amber : Theme.Slate)) g.DrawString(plays, Theme.Small, b, rx - szp.Width, 36);
            using (var b = new SolidBrush(Theme.Ivory)) g.DrawString(count, Theme.Body, b, rx - szc.Width, 13);
            // small amber dot when something plays
            if (playing > 0) using (var b = new SolidBrush(Theme.Amber)) g.FillEllipse(b, rx - szp.Width - 14, 41, 7, 7);
        }
    }
}
