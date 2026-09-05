using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KlangHub.Platform.Diagnostics;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Writing the device conversation to a file.
    /// <para>
    /// Until now it only ever reached a text box in the window, which means a dropout that happens during a
    /// forty-minute album cannot be investigated afterwards: by the time anybody looks, the evidence has
    /// scrolled away. A stability run of two speakers over fifteen minutes is exactly the case that needs
    /// a file to read back.
    /// </para>
    /// </summary>
    public class DeviceLogFileTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "klanghub-log-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a temp folder that stays is not worth failing a test over */ }
        }

        /// <summary>
        /// Reads the log the way anybody looking at it would: while KlangHub still has it open. File.ReadAllLines
        /// cannot, because it insists nobody else is writing - which is the one thing that is certainly not true
        /// about a log being written right now.
        /// </summary>
        private static string[] ReadWhileOpen(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        [Fact]
        public void Writes_a_line_with_the_time_in_front_of_it()
        {
            using var log = new DeviceLogFile(folder);
            log.Write("out [192.168.8.30:8009] [Playing]: hello");
            log.Flush();

            var line = ReadWhileOpen(log.Path).Single();

            Assert.Contains("hello", line);
            // Milliseconds, because the thing being measured is a gap of a few hundred of them.
            Assert.Matches(@"^\d{2}:\d{2}:\d{2}\.\d{3} ", line);
        }

        [Fact]
        public void Keeps_every_line_in_order()
        {
            using var log = new DeviceLogFile(folder);
            for (var i = 0; i < 50; i++)
                log.Write("line " + i);
            log.Flush();

            var lines = ReadWhileOpen(log.Path);

            Assert.Equal(50, lines.Length);
            Assert.Contains("line 0", lines[0]);
            Assert.Contains("line 49", lines[49]);
        }

        [Fact]
        public void Survives_being_written_from_several_threads_at_once()
        {
            // Every device connection logs from its own thread. A torn line would make a run unreadable
            // exactly when it matters.
            using var log = new DeviceLogFile(folder);

            Parallel.For(0, 200, i => log.Write("thread line " + i));
            log.Flush();

            var lines = ReadWhileOpen(log.Path);
            Assert.Equal(200, lines.Length);
            Assert.All(lines, l => Assert.Contains("thread line ", l));
        }

        [Fact]
        public void Starts_a_new_file_rather_than_growing_without_end()
        {
            // A long session logs a lot. Rolling over keeps a runaway log from filling a disc overnight.
            using var log = new DeviceLogFile(folder, largestFileBytes: 400);

            for (var i = 0; i < 60; i++)
                log.Write("a line that is long enough to add up quickly " + i);
            log.Flush();

            Assert.True(Directory.GetFiles(folder).Length > 1);
        }

        [Fact]
        public void A_folder_it_cannot_write_to_is_not_a_crash()
        {
            // Logging is a diagnostic. It must never be the reason the music stops.
            using var log = new DeviceLogFile("Z:\\nope\\nowhere");

            log.Write("this simply goes nowhere");
            log.Flush();
        }

        [Fact]
        public void A_line_reaches_the_disc_without_waiting_to_be_flushed()
        {
            // Measured on a real fifteen-minute run: the file sat at zero bytes the entire time, because
            // the first version buffered and only flushed on close. A log that can only be read after
            // closing the app is useless exactly when somebody wants it - during the session.
            using var log = new DeviceLogFile(folder);

            log.Write("this must be on disc immediately");

            Assert.Contains("this must be on disc immediately", ReadWhileOpen(log.Path).Single());
        }

        [Fact]
        public void Can_be_read_while_KlangHub_is_still_writing_it()
        {
            // Somebody investigating a dropout opens this file while the music is still playing. A writer
            // that locked others out would make the log useless exactly when it is wanted.
            using var log = new DeviceLogFile(folder);
            log.Write("mid-session line");
            log.Flush();

            using var reader = new FileStream(log.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Assert.True(reader.Length > 0);
        }

        [Fact]
        public void Names_the_file_after_the_day_it_covers()
        {
            using var log = new DeviceLogFile(folder);
            log.Write("x");
            log.Flush();

            Assert.Contains(DateTime.Now.ToString("yyyy-MM-dd"), Path.GetFileName(log.Path));
        }
    }
}
