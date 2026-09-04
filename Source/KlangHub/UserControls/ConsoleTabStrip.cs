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
    /// The console tab strip from the approved UI concept — a flat, borderless band of tabs where the active
    /// one is a lifted panel carrying a 2-px amber top edge.
    /// <para>
    /// It deliberately replaces the stock <see cref="TabControl"/>: that control paints a native 3D body
    /// frame around its pages which owner-drawing cannot reach (it is the source of the light hairline that
    /// framed the whole window), and its tab row is themed by the OS. Here the strip is a plain owner-drawn
    /// control and the pages are ordinary <see cref="Panel"/>s it shows/hides — so nothing native is left to
    /// bleed through.
    /// </para>
    /// </summary>
    public sealed class ConsoleTabStrip : Control
    {
        private sealed class Item
        {
            public required string Text { get; set; }
            public required Control Page { get; init; }
            public bool Shown { get; set; } = true;
            public Rectangle Bounds { get; set; }
        }

        private const int StripH = 44;   // 10 px breathing room above a 34-px tab
        private const int TabH = 34;
        private const int PadX = 18;     // text inset left/right inside a tab
        private const int MinTabW = 104;
        private const int FirstX = 14;

        private readonly List<Item> items = new();
        private int selected;
        private int hovered = -1;
        private Font tabFont;

        public event EventHandler? SelectedIndexChanged;

        public ConsoleTabStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = StripH;
            BackColor = Theme.Ink2;
            tabFont = new Font(Theme.Name.FontFamily, 9.5f, Theme.Name.Style);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tabFont.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>Registers a page. The first registered page starts selected; the rest are hidden.</summary>
        public void AddTab(Control page, string text)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = items.Count == 0;
            items.Add(new Item { Text = text, Page = page });
            Invalidate();
        }

        /// <summary>Retitles a page's tab (the strings are re-applied on every language change).</summary>
        public void SetTabText(Control page, string text)
        {
            var it = items.FirstOrDefault(i => i.Page == page);
            if (it == null || it.Text == text) return;
            it.Text = text;
            Invalidate();
        }

        /// <summary>Shows/hides a whole page (used for the optional Protokoll tab). Hiding the selected page
        /// falls back to the first visible one, so the window is never left blank.</summary>
        public void SetTabShown(Control page, bool shown)
        {
            var it = items.FirstOrDefault(i => i.Page == page);
            if (it == null || it.Shown == shown) return;
            it.Shown = shown;
            if (!shown && SelectedPage == page)
                SelectPage(items.FirstOrDefault(i => i.Shown)?.Page);
            else
                page.Visible = shown && SelectedPage == page;
            Invalidate();
        }

        public bool IsTabShown(Control page) => items.FirstOrDefault(i => i.Page == page)?.Shown ?? false;

        public Control? SelectedPage => selected >= 0 && selected < items.Count ? items[selected].Page : null;

        /// <summary>Moves the selection to the next/previous VISIBLE tab, wrapping around - the keyboard
        /// behaviour the stock TabControl gave us for free (Ctrl+Tab / Ctrl+Shift+Tab).</summary>
        public bool StepSelection(int direction)
        {
            int shown = items.Count(i => i.Shown);
            if (shown <= 1) return false;
            int idx = selected;
            for (int n = 0; n < items.Count; n++)
            {
                idx = (idx + direction + items.Count) % items.Count;
                if (items[idx].Shown) { SelectIndex(idx); return true; }
            }
            return false;
        }

        public void SelectPage(Control? page)
        {
            int idx = items.FindIndex(i => i.Page == page);
            if (idx >= 0) SelectIndex(idx);
        }

        private void SelectIndex(int idx)
        {
            if (idx < 0 || idx >= items.Count || !items[idx].Shown) return;
            selected = idx;
            for (int i = 0; i < items.Count; i++)
                items[i].Page.Visible = i == idx && items[i].Shown;
            SelectedPage?.BringToFront();
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Ink2);

            int x = FirstX;
            int top = Height - TabH;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.Shown) { it.Bounds = Rectangle.Empty; continue; }

                int w = Math.Max(MinTabW, (int)Math.Ceiling(g.MeasureString(it.Text, tabFont).Width) + PadX * 2);
                var r = new Rectangle(x, top, w, TabH);
                it.Bounds = r;
                DrawTab(g, r, it.Text, i == selected, i == hovered);
                x += w + 2;
            }

            // The strip's own floor blends into the page: draw the ink-coloured seat under the selected tab so
            // the tab and the page below read as one surface (the concept's "lifted panel" look).
            using var seat = new SolidBrush(Theme.Ink);
            var sel = selected >= 0 && selected < items.Count ? items[selected].Bounds : Rectangle.Empty;
            if (!sel.IsEmpty) g.FillRectangle(seat, sel.Left, Height - 2, sel.Width, 2);
        }

        private void DrawTab(Graphics g, Rectangle r, string text, bool active, bool hover)
        {
            var face = new RectangleF(r.X, r.Y, r.Width, r.Height + 6); // over-hang; bottom corners clipped away
            if (active)
            {
                using var path = TopRounded(face, 9f);
                using (var b = new SolidBrush(Theme.Ink)) g.FillPath(b, path);
                using (var pen = new Pen(Theme.Amber, 2f))
                    g.DrawLine(pen, r.X + 9, r.Y + 1.2f, r.Right - 9, r.Y + 1.2f);
            }
            else if (hover)
            {
                using var path = TopRounded(face, 9f);
                using var b = new SolidBrush(Theme.Blend(Theme.Ink2, Theme.Surface, 0.7f));
                g.FillPath(b, path);
            }

            var fg = active ? Theme.Ivory : hover ? Theme.Blend(Theme.Slate, Theme.Ivory, 0.5f) : Theme.Slate;
            TextRenderer.DrawText(g, text, tabFont, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        /// <summary>A rectangle rounded on the top two corners only — a tab, not a pill.</summary>
        private static GraphicsPath TopRounded(RectangleF r, float radius)
        {
            float d = radius * 2f;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hovered) { hovered = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hovered != -1) { hovered = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0) SelectIndex(h);
            base.OnMouseDown(e);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].Shown && items[i].Bounds.Contains(p)) return i;
            return -1;
        }
    }
}
