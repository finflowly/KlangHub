using Microsoft.Win32;

namespace KlangHub.Classes
{
    public static class WindowsStartup
    {
        // 2.2b-H4a: WinForms-free - the caller passes the Run-key value name + exe path and handles any
        // exception (e.g. shows a MessageBox), so this stays pure Registry code and lives in KlangHub.Platform.
        public static void StartApplicationWhenWindowsStarts(bool enable, string valueName, string exePath)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!;

            // Clean up the pre-rebrand autostart entry.
            rk.DeleteValue("Desktop Audio Streamer", false);

            if (enable)
                rk.SetValue(valueName, exePath);
            else
                rk.DeleteValue(valueName, false);
        }
    }
}
