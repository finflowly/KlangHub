using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace KlangHub.Classes
{
    /// <summary>
    /// The rooms a home is usually made of, offered as a pick-list when naming a speaker.
    /// <para>
    /// The identities are fixed and English (<see cref="Ids"/>); only the labels are translated, as ONE
    /// resource line per language whose entries line up with those identities by position. That keeps
    /// twenty rooms × twenty-four languages to a single string per language instead of 480 resource keys,
    /// and a test pins the counts together so a mistranslation cannot silently shift every room by one.
    /// </para>
    /// A room typed by hand is just as valid - the list is a shortcut, not a constraint.
    /// </summary>
    public static class RoomPresets
    {
        /// <summary>Stable identities, in the order the translated labels are listed.</summary>
        public static readonly string[] Ids =
        {
            "living", "kitchen", "bedroom", "bathroom", "office", "dining", "hallway", "kids",
            "terrace", "garden", "basement", "attic", "guest", "workshop", "loft", "wardrobe",
            "conservatory", "balcony", "garage", "party",
        };

        private static readonly string[] EnglishFallback =
        {
            "Living room", "Kitchen", "Bedroom", "Bathroom", "Office", "Dining room", "Hallway",
            "Kids' room", "Terrace", "Garden", "Basement", "Attic", "Guest room", "Workshop", "Loft",
            "Walk-in closet", "Conservatory", "Balcony", "Garage", "Party room",
        };

        /// <summary>The room names in the given (or current) language, in <see cref="Ids"/> order.</summary>
        public static string[] Labels(CultureInfo? culture = null)
        {
            var raw = Properties.Strings.ResourceManager.GetString(
                "Room_Presets_Text", culture ?? CultureInfo.CurrentUICulture);

            var parts = (raw ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();

            // A translation that does not line up is not used at all: a shifted list would label the kitchen
            // "Bathroom", which is worse than showing English.
            return parts.Length == Ids.Length ? parts : EnglishFallback;
        }

        /// <summary>The identity behind a room name, or null when the user typed their own.</summary>
        public static string? IdFor(string? roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return null;

            var name = roomName!.Trim();
            // Match against every shipped language, not just the current one: a room named while the app was
            // German keeps its icon after switching to English.
            foreach (var culture in MainForm.SupportedCultures)
            {
                var labels = Labels(CultureInfo.GetCultureInfo(culture));
                for (int i = 0; i < labels.Length && i < Ids.Length; i++)
                    if (string.Equals(labels[i], name, StringComparison.CurrentCultureIgnoreCase))
                        return Ids[i];
            }
            return null;
        }

        /// <summary>
        /// Draws the room's glyph into <paramref name="r"/>. Rooms without a drawn symbol fall back to their
        /// initial in a ring - always fitting, never a wrong picture, and it works for a name typed by hand.
        /// </summary>
        public static void DrawIcon(Graphics g, RectangleF r, string? roomName, Color color)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, Math.Max(1.3f, r.Width / 16f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            switch (IdFor(roomName))
            {
                case "living":        // a sofa
                    g.DrawRectangle(pen, x + w * .10f, y + h * .45f, w * .80f, h * .28f);
                    g.DrawLine(pen, x + w * .10f, y + h * .45f, x + w * .10f, y + h * .30f);
                    g.DrawLine(pen, x + w * .90f, y + h * .45f, x + w * .90f, y + h * .30f);
                    g.DrawLine(pen, x + w * .22f, y + h * .73f, x + w * .22f, y + h * .84f);
                    g.DrawLine(pen, x + w * .78f, y + h * .73f, x + w * .78f, y + h * .84f);
                    break;
                case "kitchen":       // a pot with a lid
                    g.DrawRectangle(pen, x + w * .18f, y + h * .42f, w * .64f, h * .38f);
                    g.DrawLine(pen, x + w * .10f, y + h * .42f, x + w * .90f, y + h * .42f);
                    g.DrawLine(pen, x + w * .50f, y + h * .42f, x + w * .50f, y + h * .28f);
                    break;
                case "bedroom":
                case "guest":         // a bed
                    g.DrawLine(pen, x + w * .10f, y + h * .35f, x + w * .10f, y + h * .74f);
                    g.DrawLine(pen, x + w * .10f, y + h * .56f, x + w * .90f, y + h * .56f);
                    g.DrawLine(pen, x + w * .90f, y + h * .56f, x + w * .90f, y + h * .74f);
                    g.DrawArc(pen, x + w * .18f, y + h * .38f, w * .26f, h * .22f, 180, 180);
                    break;
                case "bathroom":      // a shower head with drops
                    g.DrawLine(pen, x + w * .50f, y + h * .18f, x + w * .50f, y + h * .38f);
                    g.DrawArc(pen, x + w * .26f, y + h * .32f, w * .48f, h * .26f, 180, 180);
                    g.DrawLine(pen, x + w * .36f, y + h * .66f, x + w * .36f, y + h * .74f);
                    g.DrawLine(pen, x + w * .52f, y + h * .70f, x + w * .52f, y + h * .80f);
                    g.DrawLine(pen, x + w * .68f, y + h * .66f, x + w * .68f, y + h * .74f);
                    break;
                case "office":
                case "workshop":      // a desk / bench
                    g.DrawLine(pen, x + w * .10f, y + h * .48f, x + w * .90f, y + h * .48f);
                    g.DrawLine(pen, x + w * .18f, y + h * .48f, x + w * .18f, y + h * .82f);
                    g.DrawLine(pen, x + w * .82f, y + h * .48f, x + w * .82f, y + h * .82f);
                    g.DrawRectangle(pen, x + w * .30f, y + h * .26f, w * .40f, h * .22f);
                    break;
                case "dining":        // a plate between cutlery
                    g.DrawEllipse(pen, x + w * .30f, y + h * .32f, w * .40f, h * .40f);
                    g.DrawLine(pen, x + w * .16f, y + h * .28f, x + w * .16f, y + h * .76f);
                    g.DrawLine(pen, x + w * .84f, y + h * .28f, x + w * .84f, y + h * .76f);
                    break;
                case "hallway":       // a door
                    g.DrawRectangle(pen, x + w * .26f, y + h * .18f, w * .48f, h * .66f);
                    g.DrawEllipse(pen, x + w * .62f, y + h * .50f, w * .07f, h * .07f);
                    break;
                case "kids":          // a kite / star
                    g.DrawPolygon(pen, new[]
                    {
                        new PointF(x + w * .50f, y + h * .18f), new PointF(x + w * .80f, y + h * .48f),
                        new PointF(x + w * .50f, y + h * .78f), new PointF(x + w * .20f, y + h * .48f),
                    });
                    g.DrawLine(pen, x + w * .50f, y + h * .78f, x + w * .62f, y + h * .88f);
                    break;
                case "terrace":
                case "balcony":       // a parasol
                    g.DrawArc(pen, x + w * .14f, y + h * .24f, w * .72f, h * .44f, 180, 180);
                    g.DrawLine(pen, x + w * .50f, y + h * .46f, x + w * .50f, y + h * .84f);
                    break;
                case "garden":
                case "conservatory":  // a tree
                    g.DrawEllipse(pen, x + w * .26f, y + h * .18f, w * .48f, h * .44f);
                    g.DrawLine(pen, x + w * .50f, y + h * .62f, x + w * .50f, y + h * .86f);
                    break;
                case "basement":
                case "attic":         // a house outline
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + w * .14f, y + h * .52f), new PointF(x + w * .50f, y + h * .20f),
                        new PointF(x + w * .86f, y + h * .52f),
                    });
                    g.DrawRectangle(pen, x + w * .24f, y + h * .52f, w * .52f, h * .32f);
                    break;
                case "loft":          // a lofted roof with a window
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + w * .12f, y + h * .70f), new PointF(x + w * .50f, y + h * .22f),
                        new PointF(x + w * .88f, y + h * .70f),
                    });
                    g.DrawRectangle(pen, x + w * .40f, y + h * .48f, w * .20f, h * .22f);
                    break;
                case "wardrobe":      // a hanger
                    g.DrawArc(pen, x + w * .42f, y + h * .22f, w * .16f, h * .16f, 0, 360);
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + w * .50f, y + h * .38f), new PointF(x + w * .18f, y + h * .66f),
                        new PointF(x + w * .82f, y + h * .66f), new PointF(x + w * .50f, y + h * .38f),
                    });
                    break;
                case "garage":        // a roller door
                    g.DrawRectangle(pen, x + w * .14f, y + h * .30f, w * .72f, h * .52f);
                    g.DrawLine(pen, x + w * .14f, y + h * .50f, x + w * .86f, y + h * .50f);
                    g.DrawLine(pen, x + w * .14f, y + h * .66f, x + w * .86f, y + h * .66f);
                    break;
                case "party":         // a disco ball
                    g.DrawEllipse(pen, x + w * .26f, y + h * .32f, w * .48f, h * .48f);
                    g.DrawLine(pen, x + w * .50f, y + h * .14f, x + w * .50f, y + h * .32f);
                    g.DrawLine(pen, x + w * .30f, y + h * .56f, x + w * .70f, y + h * .56f);
                    break;
                default:
                    DrawMonogram(g, r, roomName, color, pen);
                    break;
            }

            g.SmoothingMode = old;
        }

        private static void DrawMonogram(Graphics g, RectangleF r, string? roomName, Color color, Pen pen)
        {
            g.DrawEllipse(pen, r.X + r.Width * .12f, r.Y + r.Height * .12f, r.Width * .76f, r.Height * .76f);
            var initial = (roomName ?? string.Empty).Trim();
            if (initial.Length == 0)
                return;

            using var font = new Font(Theme.Name.FontFamily, r.Height * 0.42f, Theme.Name.Style, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(char.ToUpper(initial[0], CultureInfo.CurrentCulture).ToString(), font, brush, r, sf);
        }
    }
}
