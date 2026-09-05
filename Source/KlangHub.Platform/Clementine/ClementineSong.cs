using System;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.Clementine
{
    /// <summary>
    /// Turns what Clementine's network remote sends into what the stage needs.
    /// <para>
    /// This is the richest source KlangHub has for a Clementine listener: the album, the length, the file
    /// on disc and the cover art as bytes, none of which a window title can carry. Measured on 2026-09-05
    /// it is also the only way to get any of it - Clementine reports nothing whatsoever to Windows' own
    /// now-playing session while it is playing.
    /// </para>
    /// </summary>
    public static class ClementineSong
    {
        public static NowPlayingTrack ToTrack(SongMetadata? song)
        {
            if (song == null)
                return new NowPlayingTrack();

            return new NowPlayingTrack
            {
                Title = Blank(song.Title),
                Artist = Blank(song.Artist) ?? Blank(song.Albumartist),
                Album = Blank(song.Album),
                // Zero is what an internet radio reports. Passing it on would draw a finished progress
                // line under something that has no end.
                Duration = song.Length > 0 ? TimeSpan.FromSeconds(song.Length) : null,
                FilePath = LocalPath(song.Url),
                Format = FormatName(song.Type),
                CoverBytes = song.Art != null && song.Art.Length > 0 ? song.Art.ToByteArray() : null
            };
        }

        /// <summary>
        /// Clementine's own file-type numbers, named the way somebody would say them out loud. Kept as a
        /// switch on the wire numbers rather than on a generated enum: the numbers are the protocol, and
        /// a value we have never seen must fall through to "no claim" rather than to a wrong name.
        /// </summary>
        private static string? FormatName(int type) => type switch
        {
            1 => "WMA",         // ASF
            2 => "FLAC",
            3 => "MP4",
            4 => "MPC",
            5 => "MP3",         // MPEG
            6 => "FLAC",        // OGGFLAC
            7 => "SPEEX",
            8 => "OGG",         // OGGVORBIS
            9 => "AIFF",
            10 => "WAV",
            11 => "TTA",        // TRUEAUDIO
            12 => "CD",         // CDDA
            13 => "OPUS",
            14 => "WAVPACK",
            17 => "APE",
            // 0 = UNKNOWN, 99 = STREAM, and anything Clementine adds later: say nothing rather than guess.
            _ => null
        };

        /// <summary>
        /// "file:///D:/Musik/03%20Nowhere.flac" back into a path the tag reader and the cover hunt can use.
        /// Anything that is not a local file - an internet radio, most obviously - yields nothing, because
        /// handing a stream URL to a tag reader is nonsense rather than a near miss.
        /// </summary>
        private static string? LocalPath(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var uri = new Uri(url);
                return uri.IsFile ? uri.LocalPath.Replace('/', '\\') : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
