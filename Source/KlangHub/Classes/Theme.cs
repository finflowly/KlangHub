using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KlangHub.Classes
{
    /// <summary>
    /// The KlangHub visual system — a warm-dark "hi-fi console" palette derived from the brand artwork
    /// (deep ink + a single warm amber accent). Central tokens + small GDI+ helpers so every owner-drawn
    /// surface (cards, sliders, meters) stays consistent.
    /// </summary>
    public static class Theme
    {
        // ---- warm dark palette (default) ----
        public static readonly Color Ink = Color.FromArgb(0x0E, 0x10, 0x14);   // base background
        public static readonly Color Ink2 = Color.FromArgb(0x0B, 0x0D, 0x11);  // recessed
        public static readonly Color Surface = Color.FromArgb(0x17, 0x1B, 0x23); // card
        public static readonly Color Raised = Color.FromArgb(0x20, 0x27, 0x2F);  // hover
        public static readonly Color Line = Color.FromArgb(0x2A, 0x32, 0x3D);    // border / divider
        public static readonly Color LineHi = Color.FromArgb(0x3A, 0x45, 0x52);  // hover border

        public static readonly Color Amber = Color.FromArgb(0xEB, 0xB6, 0x5A);   // the brand accent
        public static readonly Color AmberDim = Color.FromArgb(0xB9, 0x8B, 0x3E);
        public static readonly Color Ivory = Color.FromArgb(0xF3, 0xEC, 0xDD);   // primary text
        public static readonly Color Slate = Color.FromArgb(0x8A, 0x94, 0xA6);   // secondary text
        public static readonly Color Slate2 = Color.FromArgb(0x5E, 0x68, 0x78);

        public static readonly Color Blue = Color.FromArgb(0x6C, 0x89, 0xB8);    // connected
        public static readonly Color Ember = Color.FromArgb(0xE5, 0x73, 0x4B);   // error

        // amber at low alpha for pill fills / playing tint
        public static readonly Color AmberSoft = Color.FromArgb(36, 0xEB, 0xB6, 0x5A);
        public static readonly Color AmberTint = Color.FromArgb(16, 0xEB, 0xB6, 0x5A);

        // ---- fonts (the app's real Segoe UI Variable, falling back to Segoe UI) ----
        private static readonly string DisplayFamily = HasFamily("Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";
        private static readonly string TextFamily = HasFamily("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";

        public static readonly Font Title = new Font(DisplayFamily, 15f, FontStyle.Bold);
        public static readonly Font Name = new Font(DisplayFamily, 12f, FontStyle.Bold);
        public static readonly Font Body = new Font(TextFamily, 9.5f, FontStyle.Regular);
        public static readonly Font Small = new Font(TextFamily, 8.5f, FontStyle.Regular);
        public static readonly Font Label = new Font(TextFamily, 8f, FontStyle.Bold);
        public static readonly Font Data = new Font("Consolas", 9f, FontStyle.Regular);

        /// <summary>The currently selected stream format, shown on playing cards (e.g. "FLAC · 24-bit").
        /// One HTTP stream serves all devices, so this is app-global; MainForm updates it on format change
        /// via <see cref="FormatPillText"/>.</summary>
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

        /// <summary>The amber-ring app icon (the brand mark) from the embedded KlangHub.ico — used for the
        /// window title bar and the tray icon so the running app matches the taskbar/exe icon.</summary>
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
            try { using var f = new Font(name, 9f); return string.Equals(f.Name, name, System.StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>A rounded-rectangle path for cards, pills and buttons.</summary>
        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
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
    }
}
