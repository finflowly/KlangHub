using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// A <see cref="ComboBox"/> that owner-draws its closed field for the warm-dark theme — the one part a
    /// stock combo won't theme is the drop-down button, which the OS always paints light. This subclass
    /// over-draws that button (dark fill + an amber chevron) after every paint, so no native light chrome
    /// remains. It stays a real ComboBox, so Items / SelectedItem / SelectedIndexChanged are unchanged, and
    /// the drop-down item list keeps using MainForm's existing owner-draw.
    /// </summary>
    public sealed class DarkComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private const int ButtonW = 22;

        public DarkComboBox()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.Surface;
            ForeColor = Theme.Ivory;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && IsHandleCreated)
                OverdrawButton();
        }

        private void OverdrawButton()
        {
            using var g = Graphics.FromHwnd(Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var btn = new Rectangle(Width - ButtonW, 1, ButtonW - 1, Height - 2);
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, btn);
            using (var sep = new Pen(Theme.Line)) g.DrawLine(sep, btn.Left, 4, btn.Left, Height - 4);

            int cx = Width - ButtonW / 2 - 1, cy = Height / 2;
            using var pen = new Pen(Theme.Amber, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2.5f), new PointF(cx + 4, cy - 2) });
        }
    }
}
