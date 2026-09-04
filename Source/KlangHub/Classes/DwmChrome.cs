using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace KlangHub.Classes
{
    /// <summary>
    /// Makes the native Windows title bar match the app's dark theme via DWM window attributes, so the OS
    /// chrome doesn't clash with the dark client area (the "Aero-look" ask). Immersive dark mode is supported
    /// since Windows 10 1809; the custom caption/text/border colors need Windows 11 22H2+ - both calls are
    /// best-effort (ignored HRESULT failures on older builds, no exception).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class DwmChrome
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_FRAMECHANGED = 0x0020;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        public static void Apply(Form form, bool dark)
        {
            if (form == null || !form.IsHandleCreated) return;
            var hwnd = form.Handle;

            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            if (dark)
            {
                int caption = ToColorRef(Theme.Ink);
                int text = ToColorRef(Theme.Ivory);
                int border = ToColorRef(Theme.Ink); // border == caption == client → one seamless dark shell, no light hairline
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            else
            {
                int auto = unchecked((int)0xFFFFFFFF); // DWMWA_COLOR_DEFAULT - restore the system default
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref auto, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref auto, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref auto, sizeof(int));
            }

            // DWM doesn't always repaint the non-client frame immediately after the attribute changes (a known
            // quirk - otherwise the title bar only goes dark after the window is moved/resized); force it.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

        /// <summary>
        /// Disables OS visual-styles theming for a single control (e.g. TabControl), so its native chrome -
        /// which owner-draw only replaces the tab-item rectangles, not the surrounding themed tab-row band -
        /// stops bleeding a light strip through the dark theme. Affects only this control, not the app.
        /// </summary>
        public static void DisableVisualStyles(Control control)
        {
            if (control.IsHandleCreated) SetWindowTheme(control.Handle, string.Empty, string.Empty);
            else control.HandleCreated += (s, e) => SetWindowTheme(control.Handle, string.Empty, string.Empty);
        }

        /// <summary>
        /// Switches a scrolling control's native scroll bars to the OS dark variant ("DarkMode_Explorer"),
        /// so an AutoScroll panel stops cutting a bright white gutter down the side of the dark console.
        /// Best-effort: on builds without the dark theme class the call is a no-op and the light bar stays.
        /// </summary>
        public static void UseDarkScrollbars(Control control)
        {
            if (control == null) return;
            if (control.IsHandleCreated) SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            else control.HandleCreated += (s, e) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }
    }
}
