using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;

namespace KlangHub.Classes
{
    /// <summary>
    /// Draws the now-playing artwork the Chromecast puts full-screen on the TV.
    /// <para>
    /// It used to be a fixed PNG with an English claim burnt into it, which meant a Spanish or Greek user got
    /// a Spanish app, a Spanish installer - and an English television. The picture is therefore painted at
    /// runtime from the same tokens the app itself uses (<see cref="Theme"/>), with the tagline pulled from
    /// the resources, and cached per culture: one bitmap per language, rendered once.
    /// </para>
    /// The embedded PNG remains as a fallback for the (unlikely) case that drawing fails.
    /// </summary>
    public static class ArtworkRenderer
    {
        private const int Size = 1280;   // the size the LOAD message announces to the receiver

        private static readonly object gate = new object();
        private static string? cachedCulture;
        private static byte[]? cachedBytes;

        /// <summary>PNG bytes of the artwork for the current UI culture (cached).</summary>
        public static byte[] CurrentPng()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            lock (gate)
            {
                if (cachedBytes != null && cachedCulture == culture)
                    return cachedBytes;

                var bytes = Render();
                if (bytes.Length == 0)
                    return ArtworkImage.EmbeddedBytes;   // fall back to the shipped picture

                cachedCulture = culture;
                cachedBytes = bytes;
                return bytes;
            }
        }

        /// <summary>Drops the cache after a language change, so the next cast carries the new tagline.</summary>
        public static void Invalidate()
        {
            lock (gate)
            {
                cachedCulture = null;
                cachedBytes = null;
            }
        }

        private static byte[] Render()
        {
            try
            {
                using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    DrawBackdrop(g);
                    // scale 1 is a 34-px mark, so ~9 puts a 300-px mark in a 1280-px frame: present
                    // from across the room, still small enough for the rings to read as radiating from it
                    Theme.DrawBrandMark(g, Size * 0.5f, Size * 0.395f, 9f);
                    DrawWordmark(g);
                }

                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<byte>();
            }
        }

        private static void DrawBackdrop(Graphics g)
        {
            var full = new Rectangle(0, 0, Size, Size);
            using (var bg = new LinearGradientBrush(full, Theme.Ink2, Theme.Ink, LinearGradientMode.Vertical))
                g.FillRectangle(bg, full);

            // the sound rings, radiating from the mark - the brand's one gesture
            float cx = Size * 0.5f, cy = Size * 0.395f;
            for (int i = 1; i <= 11; i++)
            {
                float r = Size * 0.045f * i;
                int alpha = Math.Max(6, 74 - i * 6);
                using var pen = new Pen(Color.FromArgb(alpha, Theme.Amber), 1.6f);
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
            }
        }

        private static void DrawWordmark(Graphics g)
        {
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            using (var nameFont = new Font(Theme.Display.FontFamily, Size * 0.105f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var nb = new SolidBrush(Theme.Ivory))
                g.DrawString("KlangHub", nameFont, nb, new RectangleF(0, Size * 0.60f, Size, Size * 0.12f), sf);

            var tagline = Properties.Strings.Artwork_Tagline_Text;
            if (string.IsNullOrWhiteSpace(tagline))
                tagline = "LOSSLESS · WHOLE-HOME AUDIO";

            // The claim is set in tracked capitals; long translations are scaled down rather than wrapped, so
            // the line keeps its shape in every language.
            var box = new RectangleF(Size * 0.06f, Size * 0.705f, Size * 0.88f, Size * 0.06f);
            float px = Size * 0.032f;
            using (var tb = new SolidBrush(Color.FromArgb(215, Theme.Amber)))
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    using var f = new Font(Theme.Body.FontFamily, px, FontStyle.Regular, GraphicsUnit.Pixel);
                    var measured = g.MeasureString(tagline, f);
                    if (measured.Width <= box.Width || px <= Size * 0.016f)
                    {
                        g.DrawString(tagline, f, tb, box, sf);
                        break;
                    }
                    px *= box.Width / measured.Width * 0.98f;
                }
            }
        }
    }
}
