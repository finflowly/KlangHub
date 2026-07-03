using Microsoft.Win32;
using System;
using System.Windows.Forms;

namespace KlangHub.Classes
{
    public static class WindowsStartup
    {
        public static void StartApplicationWhenWindowsStarts(bool value)
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey 
                    ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                rk.DeleteValue("Desktop Audio Streamer", false);

                if (value)
                    rk.SetValue("KlangHub", System.Windows.Forms.Application.ExecutablePath);
                else
                    rk.DeleteValue("KlangHub", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
