using System;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.Clementine
{
    public static class ClementineSong
    {
        public static NowPlayingTrack ToTrack(SongMetadata? song)
        {
            if (song == null)
                return new NowPlayingTrack();

            var title = Blank(song.Title);
            var artist = Blank(song.Artist) ?? Blank(song.Albumartist);

            if (artist == null)
            {
                var split = IcyTitle.Split(title);
                if (split.Certain)
                {
                    artist = split.Artist;
                    title = split.Title;
                }
            }

            return new NowPlayingTrack
            {
                Title = title,
                Artist = artist,
                Album = Blank(song.Album),
                Duration = song.Length > 0 ? TimeSpan.FromSeconds(song.Length) : null,
                FilePath = LocalPath(song.Url),
                Format = FormatName(song.Type),
                CoverBytes = song.Art != null && song.Art.Length > 0 ? song.Art.ToByteArray() : null
            };
        }

        private static string? FormatName(int type) => type switch
        {
            1 => "WMA",
            2 => "FLAC",
            3 => "MP4",
            4 => "MPC",
            5 => "MP3",
            6 => "FLAC",
            7 => "SPEEX",
            8 => "OGG",
            9 => "AIFF",
            10 => "WAV",
            11 => "TTA",
            12 => "CD",
            13 => "OPUS",
            14 => "WAVPACK",
            17 => "APE",
            _ => null
        };

        internal static string? LocalPath(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var uri = new Uri(url);
                if (!uri.IsFile)
                    return null;

                if (uri.IsUnc)
                    return null;

                return uri.LocalPath.Replace('/', '\\');
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
