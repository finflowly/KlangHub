using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KlangHub.Classes
{
    /// <summary>
    /// Holds the claim that this process is the one KlangHub for this signed-in user.
    /// <para>
    /// The project inherited a single-instance check that was commented out. It cost nothing while closing
    /// the window ended the program; once the close button began hiding the window in the notification
    /// area, clicking the desktop icon again became the obvious thing to do - and started a second copy
    /// that fought the first for the REST port, the streaming port and the audio device. In a log it reads
    /// as one line and then nothing more: "normally only one use of each socket address is permitted".
    /// </para>
    /// <para>
    /// A named mutex rather than <c>WindowsFormsApplicationBase</c>: it is a few lines, it releases itself
    /// when the process dies however it dies, and it does not put a second message pump behind a form that
    /// already lives in the notification area.
    /// </para>
    /// </summary>
    public sealed class SingleInstance : IDisposable
    {
        /// <summary>
        /// The name of the claim. <c>Local\</c> scopes it to the logon session, so two people signed in at
        /// once on the same machine each get their own KlangHub instead of the second one silently
        /// refusing to start. The identifier is fixed and says nothing about who is running it - a kernel
        /// object name is readable by every process in the session.
        /// </summary>
        public const string Name = @"Local\KlangHub-c0ffee42-9b1e-4f7a-8a53-3d2c6b5e10a4";

        private readonly Mutex? mutex;

        private SingleInstance(Mutex? mutexIn, bool isOnlyInstance)
        {
            mutex = mutexIn;
            IsOnlyInstance = isOnlyInstance;
        }

        /// <summary>True when no other KlangHub was already running for this user.</summary>
        public bool IsOnlyInstance { get; }

        public static SingleInstance TryAcquire() => TryAcquire(Name);

        /// <summary>
        /// Claim a named instance. The application always claims <see cref="Name"/>; the parameter exists
        /// so a test can claim something of its own. Sharing the real name with the tests meant that
        /// installing KlangHub and then running the suite turned two tests red - on a build server, where
        /// the application is never running, they passed and hid it.
        /// </summary>
        public static SingleInstance TryAcquire(string name)
        {
            try
            {
                var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
                if (createdNew)
                    return new SingleInstance(mutex, true);

                mutex.Dispose();
                return new SingleInstance(null, false);
            }
            catch (Exception)
            {
                // A machine that will not give us a mutex is not a reason to refuse to play music. Better
                // two copies than none.
                return new SingleInstance(null, true);
            }
        }

        /// <summary>
        /// The message the second copy sends before it leaves, so the copy that is already running comes to
        /// the front instead of nothing appearing to happen. Registered by name, so both processes agree on
        /// the number without either of them owning it.
        /// </summary>
        public static readonly int ShowWindowMessage = RegisterWindowMessage("KlangHub.ShowExistingWindow");

        /// <summary>
        /// Ask the KlangHub that is already running to show itself. Broadcast, because the second copy has
        /// no way of knowing the first one's window handle - and every other window ignores a message
        /// number it never registered.
        /// </summary>
        public static void AskTheRunningCopyToShowItself()
        {
            const int HWND_BROADCAST = 0xFFFF;
            PostMessage((IntPtr)HWND_BROADCAST, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public void Dispose()
        {
            if (mutex == null)
                return;

            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not ours to release - nothing to undo.
            }

            mutex.Dispose();
        }
    }
}
