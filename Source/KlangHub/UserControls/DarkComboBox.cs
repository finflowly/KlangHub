using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// A <see cref="ComboBox"/> shaped like the concept's control token: a recessed ink-2 well with a 9-px
    /// radius, a hairline border and an amber chevron. Everything the OS insists on painting light — the
    /// drop-down button and the square 1-px field border — is over-drawn after each paint, and the four
    /// corners are cut back to the parent surface so the field reads as rounded.
    /// It stays a real ComboBox, so Items / SelectedItem / SelectedIndexChanged are unchanged; the closed
    /// field and the drop-down list are painted by MainForm's owner-draw.
    /// </summary>
    public sealed class DarkComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private const int ButtonW = 30;

        public DarkComboBox()
        {
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;   // frees the height from the font metrics AND lets MainForm
            ItemHeight = 24;                      // paint both the closed field and the drop-down list
            BackColor = Theme.Ink2;
            ForeColor = Theme.Ivory;
            Font = Theme.Body;
        }

        /// <summary>Clips the native border away: everything the OS paints outside this rounded region -
        /// including its light 1-px frame - simply never reaches the screen.</summary>
        public void ClipToWell()
        {
            var r = new RectangleF(2, 2, Math.Max(4, Width - 4), Math.Max(4, Height - 4));
            using var path = Theme.RoundedRect(r, Math.Max(1f, Theme.RadControl - 1f));
            Region?.Dispose();
            Region = new Region(path);
        }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); ClipToWell(); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && IsHandleCreated)
                OverdrawFrame();
        }

        private void OverdrawFrame()
        {
            using var g = Graphics.FromHwnd(Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1) the drop-down button band (always painted light by the OS) + the amber chevron
            var btn = new Rectangle(Width - ButtonW - 4, 0, ButtonW + 4, Height);  // full height: the flat
            //  drop-down button leaves a light hairline along its top edge otherwise
            using (var b = new SolidBrush(Theme.Ink2)) g.FillRectangle(b, btn);
            int cx = Width - ButtonW / 2 - 4, cy = Height / 2;
            using (var pen = new Pen(Theme.Amber, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - 4.5f, cy - 2.2f), new PointF(cx, cy + 2.8f), new PointF(cx + 4.5f, cy - 2.2f) });

            // 2) WinForms paints the flat combo's own square border from inside its WM_PAINT, so over-drawing
            //    it here only ever won on the corners. The straight edges are removed by clipping instead:
            //    ClipToWell() gives the control a rounded region 2 px inside its bounds, and the host
            //    FieldFrame draws the ink-2 well and the hairline that the concept actually calls for.
        }
    }

    /// <summary>
    /// The text-entry twin of <see cref="DarkComboBox"/>: a borderless dark <see cref="TextBox"/> that is
    /// hosted inside a <see cref="FieldFrame"/>, so the input matches the combos exactly instead of showing
    /// the stock white 3D box.
    /// </summary>
    public sealed class DarkTextBox : TextBox
    {
        public DarkTextBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Ink2;
            ForeColor = Theme.Ivory;
            Font = Theme.Body;
        }
    }
}
