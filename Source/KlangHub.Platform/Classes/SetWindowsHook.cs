using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KlangHub.Classes
{
    // 2.2b-H4a: WinForms-free (raw virtual-key codes instead of System.Windows.Forms.Keys) and App-free
    // (three neutral volume callbacks instead of IDevices), so this low-level keyboard hook lives in
    // KlangHub.Platform. The Ctrl+Alt+U / Ctrl+Alt+D / Ctrl+Alt+M chord policy is unchanged.
    public class NativeMethods
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        // Virtual-key codes. System.Windows.Forms.Keys enum values ARE these VK codes, so this is a
        // byte-for-byte equivalent of the previous (Keys)Marshal.ReadInt32 switch.
        private const int VK_U = 0x55;
        private const int VK_D = 0x44;
        private const int VK_M = 0x4D;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;   // left Alt
        private const int VK_RMENU = 0xA5;   // right Alt

        private static Action? onVolumeUp;
        private static Action? onVolumeDown;
        private static Action? onVolumeMute;
        private static readonly LowLevelKeyboardProc callbackProcedure = HookCallback;
        private static IntPtr hookId = IntPtr.Zero;
        private static bool isPressedInCtrl = false;
        private static bool isPressedInAlt = false;
        private static bool isPressedInU = false;
        private static bool isPressedInD = false;
        private static bool isPressedInM = false;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Start using the hook. The callbacks fire on the Ctrl+Alt+U/D/M chords (volume up/down/mute).
        /// </summary>
        public static void StartSetWindowsHooks(Action onVolumeUpIn, Action onVolumeDownIn, Action onVolumeMuteIn)
        {
            onVolumeUp = onVolumeUpIn;
            onVolumeDown = onVolumeDownIn;
            onVolumeMute = onVolumeMuteIn;

            try
            {
                hookId = SetHook(callbackProcedure);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Stop using the hooks.
        /// </summary>
        public static void StopSetWindowsHooks()
        {
            try
            {
                UnhookWindowsHookEx(hookId);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set the hooks on the system.
        /// </summary>
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(currentModule.ModuleName), 0);
            }
        }

        /// <summary>
        /// Callback function for the system hooks, the key combinations are detected here.
        /// </summary>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var isKeyDown = wParam == (IntPtr)WM_KEYDOWN;
            var isKeyUp = wParam == (IntPtr)WM_KEYUP;
            if (nCode >= 0 && (isKeyDown || isKeyUp))
            {
                var vk = Marshal.ReadInt32(lParam);
                switch (vk)
                {
                    case VK_U:
                        isPressedInU = isKeyDown;
                        break;
                    case VK_D:
                        isPressedInD = isKeyDown;
                        break;
                    case VK_M:
                        isPressedInM = isKeyDown;
                        break;
                    case VK_LCONTROL:
                    case VK_RCONTROL:
                        isPressedInCtrl = isKeyDown;
                        break;
                    case VK_LMENU:
                    case VK_RMENU:
                        isPressedInAlt = isKeyDown;
                        break;
                    default:
                        break;
                }
                if (isPressedInCtrl && isPressedInAlt)
                {
                    if (isPressedInU) onVolumeUp?.Invoke();
                    if (isPressedInD) onVolumeDown?.Invoke();
                    if (isPressedInM) onVolumeMute?.Invoke();
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
