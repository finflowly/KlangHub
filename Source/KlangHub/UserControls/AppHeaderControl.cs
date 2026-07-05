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

            // brand mark (concentric amber rings + glow) — the one shared brand paint (Theme.DrawBrandMark)
            int cx = 30, cy = Height / 2;
            Theme.DrawBrandMark(g, cx, cy);

            int tx = 54;
            using (var b = new SolidBrush(Theme.Ivory)) g.DrawString("KlangHub", Theme.Display, b, tx, 5);
            using (var b = new SolidBrush(Theme.Slate)) g.DrawString(Slogan, Theme.Small, b, tx + 1, 31);
        }
    }
}
