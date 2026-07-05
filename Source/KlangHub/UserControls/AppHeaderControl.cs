using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// The branded app header: the amber ring wordmark + the slogan. Docked to the top of the devices tab.
    /// The live "N Geräte · M spielen" summary lives in the row below (next to the master-volume fader), per
    /// the approved UI concept.
    /// </summary>
    public sealed class AppHeaderControl : UserControl
    {
        // The brand slogan, redefined brand-true (the artwork's rings fill every room). Kept subtle.
        private const string Slogan = "Ein Klang für jeden Raum";

        public AppHeaderControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 56;
            BackColor = Theme.Ink;
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
            int cx = 30, cy = Height / 2;
            using (var p3 = new Pen(Color.FromArgb(70, Theme.Amber), 1.4f)) g.DrawEllipse(p3, cx - 15, cy - 15, 30, 30);
            using (var p2 = new Pen(Color.FromArgb(140, Theme.Amber), 1.4f)) g.DrawEllipse(p2, cx - 9, cy - 9, 18, 18);
            using (var b = new SolidBrush(Theme.Amber)) g.FillEllipse(b, cx - 4, cy - 4, 8, 8);
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(cx - 10, cy - 10, 20, 20);
                using var pb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(90, Theme.Amber), SurroundColors = new[] { Color.FromArgb(0, Theme.Amber) } };
                g.FillPath(pb, glow);
            }

            int tx = 54;
            using (var b = new SolidBrush(Theme.Ivory)) g.DrawString("KlangHub", Theme.Title, b, tx, 6);
            using (var b = new SolidBrush(Theme.Slate)) g.DrawString(Slogan, Theme.Small, b, tx + 1, 31);
        }
    }
}
