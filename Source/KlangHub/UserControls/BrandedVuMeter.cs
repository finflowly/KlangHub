using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KlangHub.Classes;

namespace KlangHub.UserControls
{
    /// <summary>
    /// Branded input-level meter: a vertical segmented amber bar on dark that echoes the hi-fi console look,
    /// with a falling ivory peak cap. Fed a linear 0..1 peak amplitude (same feed as the old NAudio meter),
    /// smoothed on a timer so the display rises fast and decays gently.
    /// </summary>
    public sealed class BrandedVuMeter : Control
    {
        private float amplitude;
        private float smoothed;
        private float peak;
        private readonly System.Windows.Forms.Timer timer;

        public BrandedVuMeter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Ink2;
            timer = new System.Windows.Forms.Timer { Interval = 55 };
            timer.Tick += (s, e) => Animate();
            timer.Start();
            Disposed += (s, e) => timer.Dispose();
        }

        [DefaultValue(0f), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Amplitude { get => amplitude; set => amplitude = Math.Clamp(value, 0f, 1f); }

        // kept only so the designer's initializer (ported from the old NAudio VolumeMeter) still compiles
        [DefaultValue(18f), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float MaxDb { get; set; } = 18f;
        [DefaultValue(-60f), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float MinDb { get; set; } = -60f;

        private void Animate()
        {
            smoothed += (amplitude - smoothed) * (amplitude > smoothed ? 0.6f : 0.22f);
            if (smoothed >= peak) peak = smoothed; else peak = Math.Max(0f, peak - 0.018f);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Ink2);

            int segs = Math.Max(7, (Height - 6) / 5);
            float segH = (Height - 6f) / segs;
            int lit = (int)Math.Round(smoothed * segs);
            for (int i = 0; i < segs; i++)
            {
                float y = Height - 3 - (i + 1) * segH + 0.5f;
                var seg = new RectangleF(2, y, Width - 4, segH - 1.5f);
                float t = i / (float)(segs - 1);
                Color c = i < lit
                    ? (t > 0.86f ? Theme.Ember : Blend(Theme.AmberDim, Theme.Amber, Math.Min(1f, t / 0.86f)))
                    : Color.FromArgb(0x1E, 0x24, 0x2C);
                using var b = new SolidBrush(c);
                using var p = Theme.RoundedRect(seg, 1.2f);
                g.FillPath(b, p);
            }

            int peakSeg = (int)Math.Round(peak * segs);
            if (peakSeg > 0 && peakSeg <= segs)
            {
                float y = Height - 3 - peakSeg * segH + 0.5f;
                using var b = new SolidBrush(Theme.Ivory);
                g.FillRectangle(b, 2, y, Width - 4, 1.6f);
            }
        }

        private static Color Blend(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }
}
