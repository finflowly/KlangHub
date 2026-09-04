using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using KlangHub.Classes;
using KlangHub.Core.Casting;

namespace KlangHub.UserControls
{
    /// <summary>
    /// Everything a speaker will say about itself, on one card in the app's own hand.
    ///
    /// The rows come from the provider (see DeviceFacts on the Chromecast side) and are shown in the order
    /// they arrive; a fact with no source is simply absent rather than shown as an empty line, so the card
    /// is honest about how much a given device gives away. Only the audio-format row is not read from the
    /// device - the Cast sender protocol has no way to ask - and it carries a footnote saying so.
    /// </summary>
    public sealed class SpeakerDetailsPopup : Form
    {
        private const int Width_ = 460;
        private const int PadX = 22;
        private const int RowHeight = 27;

        private readonly string speakerName;
        private readonly IReadOnlyList<DeviceFact> facts;
        private readonly string? note;
        private Rectangle doneRect;

        public SpeakerDetailsPopup(string name, IReadOnlyList<DeviceFact>? details)
        {
            speakerName = name;
            facts = details ?? Array.Empty<DeviceFact>();
            note = HasKind(DeviceFactKind.AudioFormats)
                ? KlangHub.Properties.Strings.Detail_FormatsNote_Text
                : null;

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Theme.Surface;
            KeyPreview = true;

            int rows = Math.Max(facts.Count, 1);
            ClientSize = new Size(Width_, 78 + rows * RowHeight + NoteHeight() + 54);

            using var rp = Theme.RoundedRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Theme.RadCard);
            Region = new Region(rp);
        }

        private bool HasKind(DeviceFactKind kind)
        {
            foreach (var f in facts) if (f.Kind == kind) return true;
            return false;
        }

        private int noteHeight = -1;

        /// <summary>The footnote wraps, so its height depends on the text - measured once against the card's
        /// text column rather than guessed, otherwise a longer translation runs into the button.</summary>
        private int NoteHeight()
        {
            if (note == null) return 0;
            if (noteHeight < 0)
            {
                using var g = CreateGraphics();
                noteHeight = (int)Math.Ceiling(g.MeasureString(note, Theme.Small, Width_ - 2 * PadX).Height) + 14;
            }
            return noteHeight;
        }

        public void ShowAt(Point screenLocation)
        {
            var wa = Screen.FromPoint(screenLocation).WorkingArea;
            Location = new Point(
                Math.Max(wa.Left + 4, Math.Min(screenLocation.X, wa.Right - Width - 4)),
                Math.Max(wa.Top + 4, Math.Min(screenLocation.Y, wa.Bottom - Height - 4)));
            ShowDialog();
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

            using (var slate = new SolidBrush(Theme.Slate))
            using (var ivory = new SolidBrush(Theme.Ivory))
            using (var noWrap = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(KlangHub.Properties.Strings.Details_Title_Text, Theme.Label, slate, PadX, 16);
                g.DrawString(speakerName, Theme.Name, ivory, new RectangleF(PadX, 32, Width - 2 * PadX, 24), noWrap);

                int y = 78;
                if (facts.Count == 0)
                {
                    g.DrawString(KlangHub.Properties.Strings.Detail_Empty_Text, Theme.Body, slate,
                                 new RectangleF(PadX, y, Width - 2 * PadX, RowHeight));
                }

                // Label column left, value right. The value gets the wider half because the interesting ones
                // (a firmware revision, a device id) are long, and it steps down a size when it needs to
                // rather than being cut off.
                const int labelWidth = 150;
                int valueLeft = PadX + labelWidth + 12;
                int valueWidth = Width - valueLeft - PadX;

                for (int i = 0; i < facts.Count; i++)
                {
                    var fact = facts[i];
                    g.DrawString(LabelOf(fact.Kind), Theme.Small, slate,
                                 new RectangleF(PadX, y + 4, labelWidth, RowHeight), noWrap);

                    var text = ValueOf(fact);
                    var font = g.MeasureString(text, Theme.Body).Width > valueWidth ? Theme.Small : Theme.Body;
                    g.DrawString(text, font, ivory,
                                 new RectangleF(valueLeft, y + (font == Theme.Body ? 3 : 4), valueWidth, RowHeight), noWrap);

                    y += RowHeight;
                    if (i < facts.Count - 1)
                        using (var p = new Pen(Theme.Line)) g.DrawLine(p, PadX, y - 4, Width - PadX, y - 4);
                }

                if (note != null)
                {
                    y += 8;
                    g.DrawString(note, Theme.Small, slate, new RectangleF(PadX, y, Width - 2 * PadX, NoteHeight()));
                }
            }

            doneRect = new Rectangle(Width - PadX - 74, Height - 40, 74, 24);
            Theme.FillRounded(g, doneRect, Theme.RadControl, Theme.Amber);
            using (var b = new SolidBrush(Theme.OnAmber))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(KlangHub.Properties.Strings.Popup_Done_Text, Theme.Label, b, doneRect, sf);
        }

        private static string LabelOf(DeviceFactKind kind) => kind switch
        {
            DeviceFactKind.Type => KlangHub.Properties.Strings.Detail_Type_Text,
            DeviceFactKind.Model => KlangHub.Properties.Strings.Detail_Model_Text,
            DeviceFactKind.Manufacturer => KlangHub.Properties.Strings.Detail_Manufacturer_Text,
            DeviceFactKind.Firmware => KlangHub.Properties.Strings.Detail_Firmware_Text,
            DeviceFactKind.CastProtocol => KlangHub.Properties.Strings.Detail_CastProtocol_Text,
            DeviceFactKind.AudioFormats => KlangHub.Properties.Strings.Detail_AudioFormats_Text,
            DeviceFactKind.HiResAudio => KlangHub.Properties.Strings.Detail_HiResAudio_Text,
            DeviceFactKind.Multiroom => KlangHub.Properties.Strings.Detail_Multiroom_Text,
            DeviceFactKind.VolumeStep => KlangHub.Properties.Strings.Detail_VolumeStep_Text,
            DeviceFactKind.Network => KlangHub.Properties.Strings.Detail_Network_Text,
            DeviceFactKind.Signal => KlangHub.Properties.Strings.Detail_Signal_Text,
            DeviceFactKind.Address => KlangHub.Properties.Strings.Detail_Address_Text,
            DeviceFactKind.MacAddress => KlangHub.Properties.Strings.Detail_MacAddress_Text,
            DeviceFactKind.Serial => KlangHub.Properties.Strings.Detail_Serial_Text,
            DeviceFactKind.Language => KlangHub.Properties.Strings.Detail_Language_Text,
            DeviceFactKind.Uptime => KlangHub.Properties.Strings.Detail_Uptime_Text,
            DeviceFactKind.UpdatePending => KlangHub.Properties.Strings.Detail_UpdatePending_Text,
            DeviceFactKind.ReleaseTrack => KlangHub.Properties.Strings.Detail_ReleaseTrack_Text,
            _ => KlangHub.Properties.Strings.Detail_Activity_Text,
        };

        /// <summary>Turns the provider's tokens into the user's language; everything else is already a
        /// literal (a model name, an address) and is shown as it was announced.</summary>
        private static string ValueOf(DeviceFact fact) => fact.Kind switch
        {
            DeviceFactKind.Type => fact.Value switch
            {
                DeviceFact.TypeTelevision => KlangHub.Properties.Strings.Detail_TypeTv_Text,
                DeviceFact.TypeGroup => KlangHub.Properties.Strings.Detail_TypeGroup_Text,
                _ => KlangHub.Properties.Strings.Detail_TypeSpeaker_Text,
            },
            DeviceFactKind.HiResAudio or DeviceFactKind.Multiroom or DeviceFactKind.UpdatePending =>
                fact.Value == DeviceFact.Yes
                    ? KlangHub.Properties.Strings.Detail_Yes_Text
                    : KlangHub.Properties.Strings.Detail_No_Text,
            DeviceFactKind.Network when fact.Value == DeviceFact.Ethernet =>
                KlangHub.Properties.Strings.Detail_Ethernet_Text,
            DeviceFactKind.Uptime => Uptime(fact.Value),
            _ => fact.Value,
        };

        private static string Uptime(string seconds)
        {
            if (!double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                return seconds;
            var span = TimeSpan.FromSeconds(s);
            // A device that was restarted this morning should say "10 h", not "0 d 10 h".
            return span.TotalDays >= 1
                ? string.Format(CultureInfo.CurrentCulture, KlangHub.Properties.Strings.Detail_UptimeFormat_Text,
                                (int)span.TotalDays, span.Hours)
                : string.Format(CultureInfo.CurrentCulture, KlangHub.Properties.Strings.Detail_UptimeHoursFormat_Text,
                                Math.Max(1, (int)span.TotalHours));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (doneRect.Contains(e.Location)) Close();
        }

        protected override void OnMouseMove(MouseEventArgs e)
            => Cursor = doneRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Escape or Keys.Enter) Close();
            base.OnKeyDown(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            if (Visible) Close();   // clicking away dismisses it, like the options popover
            base.OnDeactivate(e);
        }
    }
}
