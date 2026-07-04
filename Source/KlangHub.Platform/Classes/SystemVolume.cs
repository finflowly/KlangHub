using System;
using System.Runtime.InteropServices;

namespace KlangHub.Classes
{
    public static class SystemVolume
    {
        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int APPCOMMAND_VOLUME_UP = 0xA0000;
        private const int APPCOMMAND_VOLUME_DOWN = 0x90000;
        private const int WM_APPCOMMAND = 0x319;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg,
            IntPtr wParam, IntPtr lParam);

        // 2.2b-H4a: platform code takes a raw window handle instead of IMainForm, so it carries no
        // App/WinForms coupling and lives in KlangHub.Platform. Callers pass IMainForm.GetHandle().
        public static void Mute(bool mute, IntPtr hWnd)
        {
            if (mute)
                Mute(hWnd);
            else
                Unmute(hWnd);
        }

        private static void Mute(IntPtr hWnd)
        {
            // Make sure the volume unmuted.
            SendMessageW(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)APPCOMMAND_VOLUME_UP);
            SendMessageW(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)APPCOMMAND_VOLUME_DOWN);
            // Then mute
            SendMessageW(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)APPCOMMAND_VOLUME_MUTE);
        }

        private static void Unmute(IntPtr hWnd)
        {
            // Make sure the volume unmuted.
            SendMessageW(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)APPCOMMAND_VOLUME_UP);
            SendMessageW(hWnd, WM_APPCOMMAND, hWnd, (IntPtr)APPCOMMAND_VOLUME_DOWN);
        }
    }
}
