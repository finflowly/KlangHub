using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// A small, themed per-speaker options popup opened from a card's overflow menu. Keeps the extra
    /// controls (currently the hard maximum-volume cap) out of the main view so the tile stays clean.
    /// </summary>
    public sealed class SpeakerOptionsPopup : Form
    {
        private readonly string speakerName;
        private int maxVolume;
        private Rectangle sliderRect, doneRect;
        private bool dragging;

        public int MaxVolume => maxVolume;

        public SpeakerOptionsPopup(string name, int currentMax)
        {
            speakerName = name;
            maxVolume = Math.Clamp(currentMax, 1, 100);

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            ClientSize = new Size(236, 150);
            KeyPreview = true;
            // clip the borderless form to a rounded card so it floats (no dark square corners behind the card)
            using (var rp = Theme.RoundedRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Theme.RadCard))
                Region = new Region(rp);
        }

        public DialogResult ShowAt(Point screenLocation)
        {
            var wa = Screen.FromPoint(screenLocation).WorkingArea;
            int x = Math.Max(wa.Left + 4, Math.Min(screenLocation.X, wa.Right - Width - 4));
            int y = Math.Max(wa.Top + 4, Math.Min(screenLocation.Y, wa.Bottom - Height - 4));
            Location = new Point(x, y);
            return ShowDialog();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Ink);

            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRounded(g, card, Theme.RadCard, Theme.Surface);
            Theme.DrawRounded(g, card, Theme.RadCard, Theme.LineHi, 1.2f);

            using (var b = new SolidBrush(Theme.Slate))
                g.DrawString("SPEAKER-OPTIONEN", Theme.Label, b, 18, 16);
            using (var b = new SolidBrush(Theme.Ivory))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(speakerName, Theme.Name, b, new RectangleF(18, 32, Width - 36, 24), sf);

            using (var b = new SolidBrush(Theme.Slate))
                g.DrawString("Max. Lautstärke (Hart-Cut)", Theme.Small, b, 18, 66);
            using (var b = new SolidBrush(Theme.Amber))
                g.DrawString(maxVolume + "%", Theme.Name, b, Width - 62, 60);

            sliderRect = new Rectangle(18, 92, Width - 36, 12);
            Theme.DrawAmberSlider(g, sliderRect, maxVolume / 100f, 14f);

            doneRect = new Rectangle(Width - 92, 116, 74, 24);
            Theme.FillRounded(g, doneRect, Theme.RadControl, Theme.Amber);
            using (var b = new SolidBrush(Theme.OnAmber))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("Fertig", Theme.Label, b, doneRect, sf);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (doneRect.Contains(e.Location)) { DialogResult = DialogResult.OK; Close(); return; }
            if (e.Y >= sliderRect.Y - 8 && e.Y <= sliderRect.Bottom + 8) { dragging = true; ApplyFromX(e.X); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) ApplyFromX(e.X);
            else Cursor = doneRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e) => dragging = false;

        private void ApplyFromX(int x)
        {
            float t = (x - sliderRect.X) / (float)sliderRect.Width;
            int v = Math.Clamp((int)Math.Round(t * 100), 1, 100);
            if (v == maxVolume) return;
            maxVolume = v;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            else if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            // clicking away commits the change (matches a lightweight popover)
            if (DialogResult == DialogResult.None && Visible) { DialogResult = DialogResult.OK; Close(); }
            base.OnDeactivate(e);
        }
    }
}
