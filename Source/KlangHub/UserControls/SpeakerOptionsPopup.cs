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
        private readonly DarkComboBox roomBox;
        private readonly FieldFrame roomFrame;

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
            ClientSize = new Size(280, 224);
            KeyPreview = true;

            // Pick a room or type one: an editable combo does both in one control. The list carries the usual
            // rooms of a home in the user's own language; anything typed by hand is just as valid, which is
            // why this is not a closed drop-down.
            roomBox = new DarkComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Text = currentRoom ?? string.Empty,
                MaxLength = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Ink2,
                ForeColor = Theme.Ivory,
                Font = Theme.Body,
                MaxDropDownItems = 10,
            };
            roomBox.Items.AddRange(RoomPresets.Labels());

            // Hosted in the same drawn well the settings page uses: a combo cannot round or recolour its own
            // border, so the well draws the shape and the combo is clipped inside it.
            roomFrame = new FieldFrame
            {
                Bounds = new Rectangle(18, 94, ClientSize.Width - 36, 36),
                Padding = new Padding(0),
            };
            roomFrame.Controls.Add(roomBox);
            Controls.Add(roomFrame);
            FitRoomBox();
            roomFrame.Resize += (s, a) => FitRoomBox();
            // clip the borderless form to a rounded card so it floats (no dark square corners behind the card)
            using (var rp = Theme.RoundedRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Theme.RadCard))
                Region = new Region(rp);
        }

        private void FitRoomBox()
        {
            // one pixel of air on each side: the combo would otherwise sit exactly on the well's hairline
            // and paint over it, leaving the field looking borderless.
            roomBox.Width = Math.Max(40, roomFrame.ClientSize.Width - 4);
            roomBox.Left = 2;
            roomBox.Top = Math.Max(0, (roomFrame.ClientSize.Height - roomBox.Height) / 2);
            roomBox.ClipToWell();
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
            roomWell = roomFrame.Bounds;
            // the room's glyph sits next to the field, so the icon the tile will carry is visible while choosing
            RoomPresets.DrawIcon(g, new RectangleF(Width - 46, 68, 26, 26), roomBox.Text,
                                 string.IsNullOrWhiteSpace(roomBox.Text) ? Theme.Slate2 : Theme.Amber);

            // --- maximum volume ---
            using (var b = new SolidBrush(Theme.Slate))
                g.DrawString(KlangHub.Properties.Strings.Popup_MaxVolume_Text, Theme.Small, b, 18, 146);
            using (var b = new SolidBrush(Theme.Amber))
                g.DrawString(maxVolume + "%", Theme.Name, b, Width - 62, 140);

            sliderRect = new Rectangle(18, 172, Width - 36, 12);
            Theme.DrawAmberSlider(g, sliderRect, maxVolume / 100f, 14f);

            doneRect = new Rectangle(Width - 92, 192, 74, 24);
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
            roomBox.GotFocus += (s, a) => { roomFrame.Focused2 = true; roomFrame.Invalidate(); Invalidate(); };
            roomBox.LostFocus += (s, a) => { roomFrame.Focused2 = false; roomFrame.Invalidate(); Invalidate(); };
            roomBox.TextChanged += (s, a) => Invalidate();
            roomBox.SelectedIndexChanged += (s, a) => Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            else if (e.KeyCode == Keys.Enter && !roomBox.DroppedDown) { DialogResult = DialogResult.OK; Close(); }
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
