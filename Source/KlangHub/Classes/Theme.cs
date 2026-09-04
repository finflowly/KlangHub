using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace KlangHub.Classes
{
    /// <summary>
    /// The KlangHub visual system — a warm-dark "hi-fi console" palette derived from the brand artwork
    /// (deep ink + a single warm amber accent). Central tokens + small GDI+ helpers so every owner-drawn
    /// surface (cards, sliders, meters, faders, popups) stays consistent. Warm-dark is the fixed identity;
    /// there is no light variant.
    /// </summary>
    public static class Theme
    {
        // ---- warm dark palette (exact concept hex) ----
        public static readonly Color Ink = Color.FromArgb(0x0E, 0x10, 0x14);   // base background
        public static readonly Color Ink2 = Color.FromArgb(0x0B, 0x0D, 0x11);  // recessed (tracks, inputs)
        public static readonly Color Surface = Color.FromArgb(0x17, 0x1B, 0x23); // card
        public static readonly Color Raised = Color.FromArgb(0x20, 0x27, 0x2F);  // hover-lifted surface
        public static readonly Color Line = Color.FromArgb(0x2A, 0x32, 0x3D);    // border / divider
        public static readonly Color LineHi = Color.FromArgb(0x3A, 0x45, 0x52);  // hover border

        public static readonly Color Amber = Color.FromArgb(0xE8, 0xB6, 0x5A);   // the brand accent (exact concept Bernstein)
        public static readonly Color AmberDim = Color.FromArgb(0xB9, 0x8B, 0x3E);
        public static readonly Color Ivory = Color.FromArgb(0xF3, 0xEC, 0xDD);   // primary text
        public static readonly Color Slate = Color.FromArgb(0x8A, 0x9A, 0xA6);   // secondary text
        public static readonly Color Slate2 = Color.FromArgb(0x5E, 0x68, 0x78);

        public static readonly Color Blue = Color.FromArgb(0x4C, 0x9E, 0xBB);    // connected (concept cyan-teal)
        public static readonly Color Ember = Color.FromArgb(0xE5, 0x73, 0x4D);   // error

        public static readonly Color MeterOff = Color.FromArgb(0x1E, 0x24, 0x2C); // unlit level-meter segment

        // amber at low alpha for pill fills / playing tint
        public static readonly Color AmberSoft = Color.FromArgb(36, 0xE8, 0xB6, 0x5A);
        public static readonly Color AmberTint = Color.FromArgb(16, 0xE8, 0xB6, 0x5A);
        public static readonly Color OnAmber = Color.FromArgb(0x19, 0x13, 0x08); // warm near-black text/glyph on amber fills

        // ---- layout metrics (8-px grid) ----
        public const int Grid = 8;
        public const int PadCard = 16;
        public const int Gap = 16;
        public const float RadCard = 14f;
        public const float RadControl = 9f;
        public const float RadPill = 999f;
        public const int HoverLift = 2;

        // ---- fonts (the app's real Segoe UI Variable) ----
        // Regular families
        private static readonly string DisplayFamily = HasFamily("Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";
        private static readonly string TextFamily = HasFamily("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
        // Semibold named instances (GDI+ has no weight axis, so we select the semibold FAMILY by name and
        // fall back to a plain-Bold stand-in when it is unavailable).
        private static readonly string DisplaySemiName =
            HasFamily("Segoe UI Variable Display Semibold") ? "Segoe UI Variable Display Semibold"
            : HasFamily("Segoe UI Semibold") ? "Segoe UI Semibold" : DisplayFamily;
        private static readonly string TextSemiName =
            HasFamily("Segoe UI Variable Text Semibold") ? "Segoe UI Variable Text Semibold"
            : HasFamily("Segoe UI Semibold") ? "Segoe UI Semibold" : TextFamily;
        private static readonly bool DisplaySemiReal = !string.Equals(DisplaySemiName, DisplayFamily, StringComparison.OrdinalIgnoreCase);
        private static readonly bool TextSemiReal = !string.Equals(TextSemiName, TextFamily, StringComparison.OrdinalIgnoreCase);

        private static Font Semi(bool display, float size)
        {
            string fam = display ? DisplaySemiName : TextSemiName;
            bool real = display ? DisplaySemiReal : TextSemiReal;
            return new Font(fam, size, real ? FontStyle.Regular : FontStyle.Bold);
        }

        public static readonly Font Display = Semi(true, 18f);  // app wordmark / big title
        public static readonly Font Title = Semi(true, 15f);    // section / dialog title
        public static readonly Font Name = Semi(true, 13f);     // device-card title
        public static readonly Font Body = new Font(TextFamily, 10.5f, FontStyle.Regular);
        public static readonly Font Small = new Font(TextFamily, 9f, FontStyle.Regular);
        public static readonly Font Label = Semi(false, 8.5f);  // tracked-caps section/status label
        public static readonly Font Data = new Font(TextFamily, 9.5f, FontStyle.Regular); // numbers (%/time)
        public static readonly Font Button = new Font(TextFamily, 9.5f, FontStyle.Regular); // pill buttons / tabs

        // ---- cached brushes/pens for the fixed theme colours (process-lifetime; owner-draw runs per frame) ----
        public static readonly SolidBrush BrushInk = new(Ink);
        public static readonly SolidBrush BrushSurface = new(Surface);
        public static readonly SolidBrush BrushAmber = new(Amber);
        public static readonly SolidBrush BrushIvory = new(Ivory);
        public static readonly SolidBrush BrushSlate = new(Slate);
        public static readonly Pen PenLine = new(Line, 1f);
        public static readonly Pen PenAmber = new(Amber, 1f);

        /// <summary>The currently selected stream format, shown on cards (e.g. "FLAC · 24-bit"). One HTTP
        /// stream serves all devices, so this is app-global; MainForm updates it on format change.</summary>
        public static string CurrentFormatLabel = "WAV · 24-bit";

        /// <summary>Short pill text for a stream format, e.g. "FLAC · 24-bit" / "WAV · 16-bit" / "MP3 · 320".</summary>
        public static string FormatPillText(KlangHub.Core.Models.SupportedStreamFormat format) => format switch
        {
            KlangHub.Core.Models.SupportedStreamFormat.Flac => "FLAC · 24-bit",
            KlangHub.Core.Models.SupportedStreamFormat.Wav_16bit => "WAV · 16-bit",
            KlangHub.Core.Models.SupportedStreamFormat.Wav_24bit => "WAV · 24-bit",
            KlangHub.Core.Models.SupportedStreamFormat.Wav_32bit => "WAV · 32-bit",
            KlangHub.Core.Models.SupportedStreamFormat.Wav => "WAV · 16-bit",
            KlangHub.Core.Models.SupportedStreamFormat.Mp3_320 => "MP3 · 320",
            KlangHub.Core.Models.SupportedStreamFormat.Mp3_128 => "MP3 · 128",
            _ => "HiFi",
        };

        // ---- the shared backdrop: the very artwork the Chromecast shows on the TV ----
        private static Image? backdrop;
        private static bool backdropTried;

        /// <summary>The branded now-playing artwork (Resources/artwork.png) - the same image the receiver
        /// renders full-screen - loaded once. Null when the resource is missing.</summary>
        public static Image? Backdrop
        {
            get
            {
                if (backdropTried) return backdrop;
                backdropTried = true;
                try
                {
                    var bytes = ArtworkImage.Bytes;
                    if (bytes.Length > 0)
                        backdrop = Image.FromStream(new System.IO.MemoryStream(bytes));
                }
                catch { backdrop = null; }
                return backdrop;
            }
        }

        /// <summary>Ink laid over the artwork. High enough that cards, text and meters keep their contrast;
        /// low enough that the rings and the warm centre glow still read as the app's own sky. The veil is
        /// graded - denser at the bottom, where the cards and the dense controls live - so the image breathes
        /// at the top and never fights the content below.</summary>
        public const int BackdropVeilTop = 192;
        public const int BackdropVeilBottom = 230;

        /// <summary>How far past a plain "cover" fit the artwork is blown up. The picture is composed for a TV
        /// (rings around the centre, wordmark below them); at 1.0 that wordmark lands right across the middle
        /// of the window and reads as a second, competing logo. Enlarging it and framing the ring band alone
        /// keeps the brand's own texture and drops the text off-canvas.</summary>
        private const float BackdropZoom = 1.9f;

        /// <summary>Where the artwork's glowing centre sits (fraction of image height). The frame is placed so
        /// the window stops just short of it: the eye gets the calm concentric ring band, not a bright disc
        /// parked behind the cards - and the wordmark, further down still, never enters the picture.</summary>
        private const float BackdropCentreFraction = 0.375f;

        /// <summary>
        /// Paints the shared backdrop into <paramref name="c"/>: ink, then the artwork scaled to COVER the
        /// whole window and anchored to the window - not to the control - so every panel that calls this
        /// shows its own slice of one continuous image, then the veil. <paramref name="scrollOffset"/> lets
        /// an AutoScroll panel cancel its scrolled origin so the backdrop stays put while content moves.
        /// </summary>
        public static void PaintBackdrop(Graphics g, Control c, Point scrollOffset = default, int blurDivisor = 1,
                                         float veilScale = 1f)
        {
            g.Clear(Ink);
            var img = Backdrop;
            var form = c.FindForm();
            if (img == null || form == null) return;

            var win = form.ClientSize;
            if (win.Width <= 0 || win.Height <= 0) return;

            // window-relative origin of this control (plus the control's own scroll offset)
            var origin = form.PointToClient(c.PointToScreen(Point.Empty));
            origin.Offset(-scrollOffset.X, -scrollOffset.Y);

            float scale = Math.Max(win.Width / (float)img.Width, win.Height / (float)img.Height) * BackdropZoom;
            float w = img.Width * scale, h = img.Height * scale;
            float x = (win.Width - w) / 2f;
            float top = Math.Max(0f, BackdropCentreFraction - win.Height / h);   // size-independent framing
            float y = -top * h;
            // never expose a bare edge: the frame always stays inside the (over-sized) image
            y = Math.Min(0f, Math.Max(y, win.Height - h));
            x -= origin.X;
            y -= origin.Y;

            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (blurDivisor <= 1)
            {
                g.DrawImage(img, x, y, w, h);
            }
            else
            {
                // GDI+ has no gaussian blur, but drawing the slice at a fraction of its size and scaling it
                // back up with bicubic interpolation IS a blur - cheap, and exactly the frosted look glass
                // needs behind it. Only this control's slice is resampled, so the cost stays tiny.
                int bw = Math.Max(1, c.ClientSize.Width / blurDivisor);
                int bh = Math.Max(1, c.ClientSize.Height / blurDivisor);
                var small = RentFrostBuffer(bw, bh);
                using (var sg = Graphics.FromImage(small))
                {
                    sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    sg.Clear(Ink);
                    sg.DrawImage(img, x / blurDivisor, y / blurDivisor, w / blurDivisor, h / blurDivisor);
                }
                g.DrawImage(small, new Rectangle(-1, -1, c.ClientSize.Width + 2, c.ClientSize.Height + 2),
                            new Rectangle(0, 0, bw, bh), GraphicsUnit.Pixel);
            }
            g.InterpolationMode = old;

            // graded veil, computed in WINDOW space so every panel's slice lines up into one gradient
            var veilRect = new RectangleF(-origin.X, -origin.Y, Math.Max(1, win.Width), Math.Max(1, win.Height));
            int vTop = (int)Math.Clamp(BackdropVeilTop * veilScale, 0, 255);
            int vBottom = (int)Math.Clamp(BackdropVeilBottom * veilScale, 0, 255);
            using (var veil = new LinearGradientBrush(veilRect,
                       Color.FromArgb(vTop, Ink), Color.FromArgb(vBottom, Ink), 90f))
                g.FillRectangle(veil, c.ClientRectangle);
        }

        // ---- glass ----
        /// <summary>How opaque a glass panel's own tint is (0-255). Low enough that the backdrop's rings
        /// travel through the card, high enough that names, meters and sliders stay perfectly legible.</summary>
        public const int GlassAlpha = 150;

        /// <summary>
        /// Paints a frosted-glass panel: a translucent tint over whatever is already on the surface, a hairline
        /// edge, and a bright top-edge highlight - the light catch that makes a pane read as glass rather than
        /// as flat transparency. Draw the (blurred) backdrop first; this only lays the pane on top.
        /// </summary>
        public static void FillGlass(Graphics g, RectangleF r, float radius, Color tint, Color edge,
                                     int alpha = GlassAlpha, float edgeWidth = 1.2f)
        {
            using (var path = RoundedRect(r, radius))
            {
                // vertical grade: light gathers at the top of a pane and falls off towards the bottom
                using var body = new LinearGradientBrush(
                    new RectangleF(r.X, r.Y - 1, Math.Max(1f, r.Width), Math.Max(1f, r.Height + 2)),
                    Color.FromArgb(Math.Min(255, alpha + 18), tint),
                    Color.FromArgb(Math.Max(0, alpha - 22), tint), 90f);
                g.FillPath(body, path);
            }

            // top highlight: a short, bright arc just inside the upper edge
            using (var hl = new GraphicsPath())
            {
                var inner = new RectangleF(r.X + 1f, r.Y + 1f, r.Width - 2f, r.Height - 2f);
                float d = Math.Min(radius, Math.Min(inner.Width, inner.Height) / 2f) * 2f;
                hl.AddArc(inner.X, inner.Y, d, d, 180, 90);
                hl.AddArc(inner.Right - d, inner.Y, d, d, 270, 90);
                using var pen = new Pen(Color.FromArgb(54, 255, 255, 255), 1.2f);
                g.DrawPath(pen, hl);
            }

            DrawRounded(g, r, radius, edge, edgeWidth);
        }

        // Every glass surface downsamples its slice of the backdrop on each paint (a playing tile repaints
        // ~11x a second), so the scratch bitmap is kept and grown instead of allocated per frame. UI-thread
        // only, which is the only thread that paints.
        private static Bitmap? frostBuffer;

        private static Bitmap RentFrostBuffer(int w, int h)
        {
            if (frostBuffer == null || frostBuffer.Width < w || frostBuffer.Height < h)
            {
                frostBuffer?.Dispose();
                frostBuffer = new Bitmap(Math.Max(w, 64), Math.Max(h, 64));
            }
            return frostBuffer;
        }

        /// <summary>
        /// A soft drop shadow under a rounded panel - concentric, fading strokes rather than a real gaussian,
        /// which is what gives a glass pane its float. Draw it before the pane itself.
        /// </summary>
        public static void DrawSoftShadow(Graphics g, RectangleF r, float radius, float spread = 5f, int alpha = 46)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int steps = Math.Max(1, (int)spread);
            for (int i = steps; i >= 1; i--)
            {
                float t = i / (float)steps;
                var rr = new RectangleF(r.X - i, r.Y - i + 1.5f, r.Width + i * 2, r.Height + i * 2);
                using var pen = new Pen(Color.FromArgb((int)(alpha * (1f - t) * (1f - t) + 3), 0, 0, 0), 2f);
                using var path = RoundedRect(rr, radius + i);
                g.DrawPath(pen, path);
            }
            g.SmoothingMode = old;
        }

        /// <summary>The amber-ring app icon (the brand mark) from the embedded KlangHub.ico.</summary>
        public static Icon? LoadAppIcon()
        {
            try
            {
                using var s = typeof(Theme).Assembly.GetManifestResourceStream("KlangHub.KlangHub.ico");
                return s != null ? new Icon(s) : null;
            }
            catch { return null; }
        }

        private static bool HasFamily(string name)
        {
            try { using var f = new Font(name, 9f); return string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        // ================= GDI+ helpers (one source of truth for every owner-drawn surface) =================

        /// <summary>Linear RGBA blend of two colours (t in 0..1).</summary>
        public static Color Blend(Color a, Color b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                a.A + (int)((b.A - a.A) * t),
                a.R + (int)((b.R - a.R) * t),
                a.G + (int)((b.G - a.G) * t),
                a.B + (int)((b.B - a.B) * t));
        }

        /// <summary>A rounded-rectangle path for cards, pills and buttons.</summary>
        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            // clamp radius so it can never exceed half the shortest side (avoids GDI+ arc artefacts on pills)
            radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f);
            float d = radius * 2f;
            var p = new GraphicsPath();
            if (radius <= 0f) { p.AddRectangle(r); p.CloseFigure(); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRounded(Graphics g, RectangleF r, float radius, Color c)
        {
            using var p = RoundedRect(r, radius);
            using var b = new SolidBrush(c);
            g.FillPath(b, p);
        }

        public static void DrawRounded(Graphics g, RectangleF r, float radius, Color c, float width = 1f)
        {
            using var p = RoundedRect(r, radius);
            using var pen = new Pen(c, width);
            g.DrawPath(pen, p);
        }

        /// <summary>A soft amber glow behind a rounded surface — the GDI+ equivalent of a coloured box-shadow.
        /// Draw this BEFORE the card body so the card sits on top of its own halo.</summary>
        public static void DrawGlow(Graphics g, RectangleF r, float radius, Color color, float spread = 16f, float maxAlpha = 0.34f)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var outer = RectangleF.Inflate(r, spread, spread);
            using var path = RoundedRect(outer, radius + spread);
            using var pgb = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb((int)(Math.Clamp(maxAlpha, 0f, 1f) * 255), color),
                SurroundColors = new[] { Color.FromArgb(0, color) },
                FocusScales = new PointF(r.Width / outer.Width, r.Height / outer.Height),
            };
            g.FillPath(pgb, path);
            g.SmoothingMode = old;
        }

        /// <summary>A 2-px amber keyboard-focus ring, inset inside the given rounded rect.</summary>
        public static void DrawFocusRing(Graphics g, RectangleF r, float radius)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rr = RectangleF.Inflate(r, -1.5f, -1.5f);
            using var p = RoundedRect(rr, Math.Max(1f, radius - 1.5f));
            using var pen = new Pen(Amber, 2f);
            g.DrawPath(pen, p);
            g.SmoothingMode = old;
        }

        /// <summary>The one KlangHub fader: recessed track + amber-gradient fill to <paramref name="level"/>
        /// (0..1) + an ivory thumb ringed in amber. Used by the master fader and per-speaker sliders.</summary>
        public static void DrawAmberSlider(Graphics g, RectangleF track, float level, float thumb = 12f)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            level = Math.Clamp(level, 0f, 1f);
            float ty = track.Y + track.Height / 2f;
            var rTrack = new RectangleF(track.X, ty - 2.5f, track.Width, 5f);
            FillRounded(g, rTrack, 2.5f, Ink2);
            DrawRounded(g, rTrack, 2.5f, Line, 1f);
            float fillW = track.Width * level;
            if (fillW > 1f)
            {
                var rFill = new RectangleF(track.X, ty - 2.5f, fillW, 5f);
                using var p = RoundedRect(rFill, 2.5f);
                using var br = new LinearGradientBrush(new RectangleF(track.X, ty - 3f, Math.Max(1f, track.Width), 6f), AmberDim, Amber, 0f);
                g.FillPath(br, p);
            }
            float cx = track.X + fillW;
            var thumbRect = new RectangleF(cx - thumb / 2f, ty - thumb / 2f, thumb, thumb);
            using (var b = new SolidBrush(Ivory)) g.FillEllipse(b, thumbRect);
            using (var pen = new Pen(Color.FromArgb(80, Amber), 3f)) g.DrawEllipse(pen, thumbRect);
            g.SmoothingMode = old;
        }

        /// <summary>The KlangHub brand mark: concentric amber rings + a glowing centre dot, centred at
        /// (cx, cy). <paramref name="scale"/> 1 ≈ a 34-px mark.</summary>
        public static void DrawBrandMark(Graphics g, float cx, float cy, float scale = 1f)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r1 = 15f * scale, r2 = 9f * scale, r3 = 4f * scale, glow = 10f * scale;
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - glow, cy - glow, glow * 2, glow * 2);
                using var pb = new PathGradientBrush(gp)
                {
                    CenterColor = Color.FromArgb(90, Amber),
                    SurroundColors = new[] { Color.FromArgb(0, Amber) },
                };
                g.FillPath(pb, gp);
            }
            using (var p = new Pen(Color.FromArgb(70, Amber), 1.4f * scale)) g.DrawEllipse(p, cx - r1, cy - r1, r1 * 2, r1 * 2);
            using (var p = new Pen(Color.FromArgb(210, Amber), 1.4f * scale)) g.DrawEllipse(p, cx - r2, cy - r2, r2 * 2, r2 * 2);
            using (var b = new SolidBrush(Amber)) g.FillEllipse(b, cx - r3, cy - r3, r3 * 2, r3 * 2);
            g.SmoothingMode = old;
        }

        /// <summary>A hand-vectored speaker glyph (optionally muted) drawn to fit <paramref name="r"/>.</summary>
        public static void DrawSpeakerGlyph(Graphics g, RectangleF r, Color c, bool muted = false)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            var body = new[]
            {
                new PointF(x + w * 0.05f, y + h * 0.38f), new PointF(x + w * 0.26f, y + h * 0.38f),
                new PointF(x + w * 0.48f, y + h * 0.18f), new PointF(x + w * 0.48f, y + h * 0.82f),
                new PointF(x + w * 0.26f, y + h * 0.62f), new PointF(x + w * 0.05f, y + h * 0.62f),
            };
            g.DrawPolygon(pen, body);
            if (muted)
            {
                g.DrawLine(pen, x + w * 0.62f, y + h * 0.34f, x + w * 0.92f, y + h * 0.66f);
                g.DrawLine(pen, x + w * 0.92f, y + h * 0.34f, x + w * 0.62f, y + h * 0.66f);
            }
            else
            {
                g.DrawArc(pen, x + w * 0.5f, y + h * 0.28f, w * 0.46f, h * 0.44f, -55, 110);
            }
            g.SmoothingMode = old;
        }

        /// <summary>Draws text with manual inter-glyph spacing (GDI+ DrawString has no letter-spacing), used
        /// for the concept's tracked uppercase section labels. Returns the total drawn width.</summary>
        public static float DrawTrackedLabel(Graphics g, string text, Font font, Color color, float x, float y, float tracking = 2f)
        {
            var oldHint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using var b = new SolidBrush(color);
            using var sf = new StringFormat(StringFormat.GenericTypographic);
            float cx = x;
            foreach (char ch in text)
            {
                string s = ch.ToString();
                g.DrawString(s, font, b, cx, y, sf);
                cx += g.MeasureString(s, font, PointF.Empty, sf).Width + tracking;
            }
            g.TextRenderingHint = oldHint;
            return cx - tracking - x;
        }

        /// <summary>Measures the width of a tracked label without drawing (for right-alignment / hit-tests).</summary>
        public static float MeasureTrackedLabel(Graphics g, string text, Font font, float tracking = 2f)
        {
            using var sf = new StringFormat(StringFormat.GenericTypographic);
            float w = 0f;
            foreach (char ch in text)
                w += g.MeasureString(ch.ToString(), font, PointF.Empty, sf).Width + tracking;
            return Math.Max(0f, w - tracking);
        }
    }
}
