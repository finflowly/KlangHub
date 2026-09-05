using System;
using System.IO;
using System.Linq;

namespace KlangHub.Platform.NowPlaying
{
    /// <summary>
    /// Looks for a picture to put behind the piece that is playing.
    /// <para>
    /// The rule from the prompt is absolute: a generic placeholder must never stand while a real cover
    /// exists somewhere. So this looks inside the file first, then beside it, and returns nothing only when
    /// there is genuinely nothing - at which point the caller keeps the branded artwork, which is a
    /// deliberate screen rather than a broken one.
    /// </para>
    /// </summary>
    public static class CoverHunt
    {
        /// <summary>
        /// The names album art is saved under, in the order they deserve. "cover" and "folder" are what
        /// rippers and players write; "front" and "artwork" are what people write by hand.
        /// </summary>
        private static readonly string[] CoverNames = { "cover", "folder", "front", "artwork", "album" };

        private static readonly string[] PictureExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        /// <summary>
        /// Whether the bytes actually begin like a picture. Checking the signature rather than the size is
        /// what tells a real cover from the empty "cover.jpg" a ripper left behind - and it does not throw
        /// away a small but perfectly good image the way a size threshold would.
        /// </summary>
        private static bool LooksLikeAPicture(byte[]? data)
        {
            if (data == null || data.Length < 12)
                return false;

            // JPEG: FF D8 FF
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return true;

            // PNG: 89 P N G CR LF 1A LF
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return true;

            // GIF87a / GIF89a
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
                return true;

            // WebP: "RIFF" .... "WEBP"
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return true;

            // BMP
            return data[0] == 0x42 && data[1] == 0x4D;
        }

        /// <summary>The picture bytes, or null when nothing was found.</summary>
        public static byte[]? Find(string? audioFilePath)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
                return null;

            return Embedded(audioFilePath) ?? BesideTheFile(audioFilePath);
        }

        /// <summary>
        /// The picture inside the file. Preferred over the folder because it belongs to this track - on a
        /// compilation the folder cover and the track cover are not the same picture.
        /// </summary>
        private static byte[]? Embedded(string path)
        {
            try
            {
                var track = new ATL.Track(path);
                var picture = track.EmbeddedPictures?.FirstOrDefault();
                var data = picture?.PictureData;
                return LooksLikeAPicture(data) ? data : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[]? BesideTheFile(string path)
        {
            try
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(path));
                if (folder == null || !Directory.Exists(folder))
                    return null;

                foreach (var name in CoverNames)
                {
                    foreach (var file in Directory.EnumerateFiles(folder))
                    {
                        if (!string.Equals(Path.GetFileNameWithoutExtension(file), name, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!PictureExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                            continue;

                        var data = ReadPicture(file);
                        if (data != null)
                            return data;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[]? ReadPicture(string path)
        {
            try
            {
                // An empty "cover.jpg" is a leftover, not a picture. Sending it would put a broken image on
                // the television, which is worse than the branded fallback.
                var data = File.ReadAllBytes(path);
                return LooksLikeAPicture(data) ? data : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
