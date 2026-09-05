using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace KlangHub.Platform.Diagnostics
{
    /// <summary>
    /// Writes the device conversation to a file so it can be read back afterwards.
    /// <para>
    /// The log used to exist only as a text box in the window, which is enough to watch something happen
    /// and useless for finding out why something happened forty minutes ago. A dropout in the middle of an
    /// album is exactly the case that needs a file: by the time anybody notices, the lines that explain it
    /// have scrolled away.
    /// </para>
    /// <para>
    /// Every rule in here follows from one thing: this is a diagnostic, and a diagnostic must never be the
    /// reason the music stops. It swallows its own failures, it never blocks a device thread for long, and
    /// it refuses to grow without end.
    /// </para>
    /// </summary>
    public sealed class DeviceLogFile : IDisposable
    {
        /// <summary>Roll over at this size so an overnight session cannot fill a disc.</summary>
        private const long DefaultLargestFileBytes = 16 * 1024 * 1024;

        private readonly string folder;
        private readonly long largestFileBytes;
        private readonly object gate = new();
        private StreamWriter? writer;
        private long written;
        private int rollover;
        private bool disposed;
        private bool broken;

        public DeviceLogFile(string folderIn, long largestFileBytes = DefaultLargestFileBytes)
        {
            folder = folderIn;
            this.largestFileBytes = largestFileBytes;
            Path = string.Empty;
        }

        /// <summary>The file currently being written, or empty before the first line.</summary>
        public string Path { get; private set; }

        public void Write(string message)
        {
            if (disposed || broken || message == null)
                return;

            lock (gate)
            {
                try
                {
                    if (writer == null || written >= largestFileBytes)
                        Open();

                    if (writer == null)
                        return;

                    // Milliseconds, because what is being measured is a gap of a few hundred of them.
                    var line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message;
                    writer.WriteLine(line);
                    written += line.Length + 2;
                }
                catch (Exception)
                {
                    // A disc that filled up, a folder that vanished, a permission that changed: from here on
                    // this log is silent rather than throwing on every device message for the rest of the
                    // evening.
                    broken = true;
                }
            }
        }

        public void Flush()
        {
            lock (gate)
            {
                try { writer?.Flush(); } catch (Exception) { broken = true; }
            }
        }

        private void Open()
        {
            try { writer?.Dispose(); } catch (Exception) { /* replacing it either way */ }
            writer = null;

            Directory.CreateDirectory(folder);

            var day = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var name = rollover == 0 ? $"klanghub-{day}.log" : $"klanghub-{day}-{rollover}.log";
            rollover++;

            Path = System.IO.Path.Combine(folder, name);
            var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            // AutoFlush ON. The first version buffered and flushed on close, on the theory that a device
            // conversation is too chatty for a write per line. Measured on a real run: four devices
            // produce about ten lines every fifteen seconds - not chatty at all - and the file sat at
            // zero bytes for the whole session, which is exactly when somebody wants to read it. A log
            // you can only read after closing the app is not a diagnostic.
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            written = stream.Length;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;

                disposed = true;
                try { writer?.Flush(); writer?.Dispose(); } catch (Exception) { /* nothing left to save it for */ }
                writer = null;
            }
        }
    }
}
