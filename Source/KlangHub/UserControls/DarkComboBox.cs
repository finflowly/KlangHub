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
    /// It stays a real ComboBox, so Items / SelectedItem / SelectedIndexChanged are unchanged; it paints its
    /// own closed field and drop-down rows, so it works anywhere - including the speaker popup.
    /// </summary>
    public sealed class DarkComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private const int ButtonW = 30;

        /// <summary>
        /// Paints both surfaces this control has: the closed field (a recessed ink-2 well) and the drop-down
        /// rows (a raised list with an amber marker on the highlighted one).
        /// <para>
        /// The closed field takes its text from <see cref="ComboBox.Text"/>, not from
        /// <c>Items[e.Index]</c>. With <c>OwnerDrawFixed</c> nobody else draws it, and the index is -1
        /// whenever nothing is selected - while the items are being refilled, or in an editable combo where
        /// the user typed something that is not in the list. Reading the index there is what made settings
        /// fields go blank.
        /// </para>
        /// </summary>
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool isField = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var bounds = isField ? ClientRectangle : e.Bounds;

            var back = isField ? Theme.Ink2 : selected ? Theme.Raised : Theme.Surface;
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, bounds);
            if (selected && !isField)
                using (var accent = new SolidBrush(Theme.Amber))
                    e.Graphics.FillRectangle(accent, bounds.X, bounds.Y, 3, bounds.Height);

            string text = isField
                ? Text ?? string.Empty
                : e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) ?? string.Empty : string.Empty;

            if (!string.IsNullOrEmpty(text))
            {
                var r = new Rectangle(bounds.X + 11, bounds.Y, bounds.Width - (isField ? 44 : 20), bounds.Height);
                TextRenderer.DrawText(e.Graphics, text, Font, r, Theme.Ivory,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix);
            }

            base.OnDrawItem(e);
        }

        /// <summary>The closed field shows <see cref="ComboBox.Text"/>, so a changed selection has to repaint
        /// it - the control would otherwise keep showing the previous entry until something else invalidates.</summary>
        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

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
            // Full height AND a little wider than the OS button: an editable combo draws a narrower button
            // than a drop-down list, and the leftover edge showed through as a stray light sliver.
            var btn = new Rectangle(Width - ButtonW - 6, 0, ButtonW + 6, Height);
            using (var b = new SolidBrush(Theme.Ink2)) g.FillRectangle(b, btn);
            int cx = Width - ButtonW / 2 - 6, cy = Height / 2;
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
