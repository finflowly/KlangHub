using System;
using System.IO;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// Reads ID3, Vorbis, MP4 and FLAC tags straight off the file - the most honest source there is, because
    /// it is what is written on the disc rather than what some player decided to say about it. It is also
    /// the only source that knows the codec, the sample rate and the bit depth.
    /// <para>
    /// Uses z440.atl.core (MIT). Deliberately not TagLib#, which is LGPL: the project already carries one
    /// LGPL exception for the FLAC encoder and that list should not grow (docs/THIRD-PARTY-LICENSES.md).
    /// </para>
    /// </summary>
    public static class FileTagReader
    {
        public static NowPlayingTrack Read(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new NowPlayingTrack();

            try
            {
                var track = new ATL.Track(path);

                // A file with no audio in it is not a track, whatever its extension claims. The path came
                // from a now-playing file the user wrote by hand, so it may point at anything at all.
                if (track.Duration <= 0 && track.SampleRate <= 0)
                    return new NowPlayingTrack();

                return new NowPlayingTrack
                {
                    // ATL falls back to the file name when a file has no title tag. That guess belongs to
                    // the file-name source and its lower rank - handing it over as a tag would let it
                    // outrank answers that actually know something.
                    Title = Tagged(track.Title, path),
                    Artist = Blank(track.Artist),
                    Album = Blank(track.Album),
                    Duration = track.Duration > 0 ? TimeSpan.FromSeconds(track.Duration) : null,
                    Format = Blank(track.AudioFormat?.ShortName),
                    SampleRate = track.SampleRate > 0 ? (int)track.SampleRate : null,
                    BitDepth = track.BitDepth > 0 ? track.BitDepth : null,
                    FilePath = path
                };
            }
            catch (Exception)
            {
                // Corrupt tags, a file being written, a share that vanished: all the same here - we learn
                // nothing from this file, and the cascade keeps whatever it already knew.
                return new NowPlayingTrack();
            }
        }

        /// <summary>Rejects a "title" that is only the file name coming back at us.</summary>
        private static string? Tagged(string? title, string path)
        {
            var trimmed = Blank(title);
            if (trimmed == null)
                return null;

            var fileName = Path.GetFileNameWithoutExtension(path);
            return string.Equals(trimmed, fileName, StringComparison.OrdinalIgnoreCase) ? null : trimmed;
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
