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
                // Quoted. Without the quotes, CreateProcess treats every space in the path as a possible
                // end of the program name and tries each prefix in turn, adding ".exe" - so for anyone
                // whose profile folder contains a space, Windows looks for a program named after the
                // first word of it before it looks for ours. On a machine whose permissions have been
                // loosened over the years, that is somebody else's program started as this user at every
                // sign-in, and nobody would ever think to look there.
                rk.SetValue(valueName, "\"" + exePath + "\"");
            else
                rk.DeleteValue(valueName, false);
        }
    }
}
