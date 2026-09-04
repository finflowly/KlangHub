using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// A rounded "surface" card — the same body the device tiles use, reused as the container for the
    /// settings sections so the Einstellungen page is built from the same material as the Räume page
    /// (concept section B: one token set across cards, options and log).
    /// Optionally carries a tracked-caps amber section title inside its top inset.
    /// </summary>
    public class CardPanel : Panel
    {
        /// <summary>Tracked uppercase caption drawn inside the card's top inset (empty = no caption).</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Caption { get; set; } = string.Empty;

        /// <summary>Optional quiet line under the caption.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Subcaption { get; set; } = string.Empty;

        /// <summary>Ink behind the rounded corners — set when the card sits on a non-ink surface.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OuterColor { get; set; } = Theme.Ink;

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;   // children with a transparent back-colour inherit the card face
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            // same frosted material as the device tiles, so both pages read as one system
            Theme.PaintBackdrop(g, this, blurDivisor: 6, veilScale: 0.82f);

            var body = new RectangleF(2.5f, 2.5f, Width - 5f, Height - 6f);
            Theme.DrawSoftShadow(g, body, Theme.RadCard, 3f, 40);
            Theme.FillGlass(g, body, Theme.RadCard, Theme.Surface, Color.FromArgb(150, Theme.LineHi));

            if (!string.IsNullOrEmpty(Caption))
            {
                Theme.DrawTrackedLabel(g, Caption.ToUpperInvariant(), Theme.Label, Theme.Amber,
                    Padding.Left, 16f, 1.6f);
                if (!string.IsNullOrEmpty(Subcaption))
                    using (var b = new SolidBrush(Theme.Slate))
                        g.DrawString(Subcaption, Theme.Small, b, Padding.Left - 2f, 32f);
            }
        }
    }

    /// <summary>A hairline row separator inside a card - its own type so the generic theme cascade, which
    /// repaints stray panels in ink, leaves it alone.</summary>
    public sealed class HairLine : Panel
    {
        public HairLine()
        {
            Dock = DockStyle.Top;
            Height = 1;
            Margin = new Padding(0);
            BackColor = Theme.Blend(Theme.Surface, Theme.Line, 0.55f);
        }
    }

    /// <summary>
    /// The recessed field well behind an input (combo / text box): ink-2 fill, 1-px line, 9-px radius —
    /// the concept's <c>--r-ctl</c> control shape. The real input sits inside with its own border removed,
    /// so no native light chrome survives anywhere on the page.
    /// </summary>
    public sealed class FieldFrame : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Focused2 { get; set; }

        public FieldFrame()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Ink2;
            Padding = new Padding(11, 6, 11, 6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Theme.FillRounded(g, r, Theme.RadControl, Theme.Ink2);
            Theme.DrawRounded(g, r, Theme.RadControl, Focused2 ? Theme.Amber : Theme.Line);
        }
    }
}
