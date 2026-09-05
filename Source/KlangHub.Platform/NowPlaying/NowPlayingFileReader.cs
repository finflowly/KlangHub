using System;
using System.IO;
using System.Text;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// Gets the now-playing text off the disc and hands it to <see cref="NowPlayingText"/>.
    /// <para>
    /// Two things make this less trivial than it looks. The encoding is whatever the helper chose - a
    /// PowerShell script writes UTF-16 by default, and reading that as UTF-8 puts line noise on the
    /// television. And the helper usually still has the file open, so it has to be read in a way that does
    /// not fight the writer for it.
    /// </para>
    /// </summary>
    public static class NowPlayingFileReader
    {
        /// <summary>A now-playing file is a few lines. Anything larger is not one, and is not read.</summary>
        private const int LargestSensibleFile = 64 * 1024;

        public static NowPlayingTrack Read(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new NowPlayingTrack();

            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.Length > LargestSensibleFile)
                    return new NowPlayingTrack();

                // FileShare.ReadWrite | Delete: the helper is very likely writing this file right now, and
                // a reader that insists on exclusive access would only ever see it between updates.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                // detectEncodingFromByteOrderMarks catches UTF-16 and UTF-8-with-BOM; UTF-8 without one is
                // the fallback, which is what a plain text file written today almost always is.
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                return NowPlayingText.Parse(reader.ReadToEnd());
            }
            catch (Exception)
            {
                // Locked, deleted between the check and the read, on a share that just went away: all of it
                // means the same thing here - we learn nothing this time, and the stage keeps what it had.
                return new NowPlayingTrack();
            }
        }
    }
}
