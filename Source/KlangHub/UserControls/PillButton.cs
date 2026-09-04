using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// The one KlangHub button: a rounded 9-px surface with a hairline border, an ivory semibold label and a
    /// warm hover lift. It <b>is</b> a <see cref="Button"/>, so Text / Click / Enabled bindings are unchanged;
    /// only the stock grey Windows chrome goes away.
    /// </summary>
    public sealed class PillButton : Button
    {
        private bool hover, down;

        /// <summary>Amber-filled emphasis variant (used sparingly — one per view at most).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Primary { get; set; }

        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.Button;   // shared, process-lifetime font - no per-instance GDI object to leak
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var bg = Theme.Ink;
            for (var p = Parent; p != null; p = p.Parent)
                if (p.BackColor.A == 255) { bg = p.BackColor; break; }
            g.Clear(bg);

            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color face, edge, text;
            if (Primary)
            {
                face = down ? Theme.AmberDim : hover ? Theme.Blend(Theme.Amber, Color.White, 0.08f) : Theme.Amber;
                edge = face;
                text = Theme.OnAmber;
            }
            else
            {
                face = !Enabled ? Theme.Ink2 : down ? Theme.Ink2 : hover ? Theme.Raised : Theme.Surface;
                edge = !Enabled ? Theme.Line : hover ? Theme.LineHi : Theme.Line;
                text = Enabled ? Theme.Ivory : Theme.Slate2;
            }

            Theme.FillRounded(g, r, Theme.RadControl, face);
            Theme.DrawRounded(g, r, Theme.RadControl, edge);

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)   // keyboard navigation only - not on the app's first paint
                Theme.DrawFocusRing(g, r, Theme.RadControl);
        }
    }
}
