using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// An owner-drawn toggle switch that <b>is</b> a <see cref="CheckBox"/> — so every existing binding on the
    /// settings page (Checked, CheckedChanged, Text, Dock, AutoSize) keeps working unchanged; only the look
    /// changes to a premium amber switch with the label on the left and the switch on the right. Replaces the
    /// stock Windows check glyph that made the page read as dated.
    /// </summary>
    public sealed class ToggleSwitch : CheckBox
    {
        private const int SwW = 42, SwH = 22, RowH = 34;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            Height = RowH;
        }

        public override Size GetPreferredSize(Size proposedSize)
            => new Size(proposedSize.Width > 0 ? proposedSize.Width : Width, RowH);

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // owner-paint the ancestor's background (walk up past transparent panels)
            var bg = Theme.Ink;
            for (var p = Parent; p != null; p = p.Parent)
                if (p.BackColor.A == 255) { bg = p.BackColor; break; }
            using (var bb = new SolidBrush(bg)) g.FillRectangle(bb, ClientRectangle);

            int sx = Width - SwW - 2;
            int sy = (Height - SwH) / 2;
            var track = new RectangleF(sx, sy, SwW, SwH);
            bool on = Checked;

            Theme.FillRounded(g, track, SwH / 2f, on ? Color.FromArgb(42, Theme.Amber) : Theme.Ink2);
            Theme.DrawRounded(g, track, SwH / 2f, on ? Color.FromArgb(160, Theme.Amber) : (Hovered ? Theme.LineHi : Theme.Line));

            int kd = SwH - 6;
            float kx = on ? sx + SwW - kd - 3 : sx + 3;
            var knob = new RectangleF(kx, sy + 3, kd, kd);
            using (var kb = new SolidBrush(on ? Theme.Amber : Theme.Slate)) g.FillEllipse(kb, knob);
            if (on)
                using (var glow = new Pen(Color.FromArgb(70, Theme.Amber), 2f)) g.DrawEllipse(glow, knob);

            var textRect = new Rectangle(2, 0, sx - 12, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Theme.Ivory : Theme.Slate,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (Focused)
                Theme.DrawFocusRing(g, new RectangleF(track.X - 3, track.Y - 3, track.Width + 6, track.Height + 6), SwH / 2f + 3);
        }

        private bool Hovered => ClientRectangle.Contains(PointToClient(MousePosition));
    }
}
