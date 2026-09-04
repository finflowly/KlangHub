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
        private Rectangle sliderRect, doneRect, roomWell;
        private bool dragging;
        private readonly TextBox roomBox;

        public int MaxVolume => maxVolume;

        /// <summary>The room the user typed (empty string = no room).</summary>
        public string Room => roomBox.Text.Trim();

        public SpeakerOptionsPopup(string name, int currentMax, string? currentRoom)
        {
            speakerName = name;
            maxVolume = Math.Clamp(currentMax, 1, 100);

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            ClientSize = new Size(260, 216);
            KeyPreview = true;

            // A real TextBox, stripped of its border and dropped into the drawn well below - the same field
            // material the settings page uses, so the popover belongs to the same system.
            roomBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Ink2,
                ForeColor = Theme.Ivory,
                Font = Theme.Body,
                Text = currentRoom ?? string.Empty,
                MaxLength = 40,
                Bounds = new Rectangle(30, 103, ClientSize.Width - 60, 20),
            };
            Controls.Add(roomBox);
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
                g.DrawString(KlangHub.Properties.Strings.Popup_SpeakerOptions_Text, Theme.Label, b, 18, 16);
            using (var b = new SolidBrush(Theme.Ivory))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(speakerName, Theme.Name, b, new RectangleF(18, 32, Width - 36, 24), sf);

            // --- room ---
            using (var b = new SolidBrush(Theme.Slate))
                g.DrawString(KlangHub.Properties.Strings.Popup_Room_Text, Theme.Small, b, 18, 76);
            roomWell = new Rectangle(18, 96, Width - 36, 34);
            Theme.FillRounded(g, roomWell, Theme.RadControl, Theme.Ink2);
            Theme.DrawRounded(g, roomWell, Theme.RadControl, roomBox.Focused ? Theme.Amber : Theme.Line);
            if (roomBox.Text.Length == 0 && !roomBox.Focused)
                using (var b = new SolidBrush(Theme.Slate2))
                    g.DrawString(KlangHub.Properties.Strings.Popup_RoomHint_Text, Theme.Small, b, 30, 105);

            // --- maximum volume ---
            using (var b = new SolidBrush(Theme.Slate))
                g.DrawString(KlangHub.Properties.Strings.Popup_MaxVolume_Text, Theme.Small, b, 18, 140);
            using (var b = new SolidBrush(Theme.Amber))
                g.DrawString(maxVolume + "%", Theme.Name, b, Width - 62, 134);

            sliderRect = new Rectangle(18, 166, Width - 36, 12);
            Theme.DrawAmberSlider(g, sliderRect, maxVolume / 100f, 14f);

            doneRect = new Rectangle(Width - 92, 186, 74, 24);
            Theme.FillRounded(g, doneRect, Theme.RadControl, Theme.Amber);
            using (var b = new SolidBrush(Theme.OnAmber))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(KlangHub.Properties.Strings.Popup_Done_Text, Theme.Label, b, doneRect, sf);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (doneRect.Contains(e.Location)) { DialogResult = DialogResult.OK; Close(); return; }
            if (roomWell.Contains(e.Location)) { roomBox.Focus(); Invalidate(); return; }
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

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            roomBox.GotFocus += (s, a) => Invalidate();
            roomBox.LostFocus += (s, a) => Invalidate();
            roomBox.TextChanged += (s, a) => Invalidate();
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
