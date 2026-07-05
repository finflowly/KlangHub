using System;
using System.IO;
using System.Reflection;

namespace KlangHub.Classes
{
    /// <summary>
    /// Loads the branded now-playing PNG (embedded resource) once and hands the bytes to the streaming server
    /// so the Chromecast can fetch it for a premium full-screen receiver image.
    /// </summary>
    public static class ArtworkImage
    {
        private const string ResourceName = "KlangHub.Resources.artwork.png";
        private static byte[]? cached;

        /// <summary>The PNG bytes, or an empty array if the resource is missing (never throws).</summary>
        public static byte[] Bytes
        {
            get
            {
                if (cached != null)
                    return cached;

                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using var stream = asm.GetManifestResourceStream(ResourceName);
                    if (stream == null)
                        return cached = Array.Empty<byte>();

                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return cached = ms.ToArray();
                }
                catch (Exception)
                {
                    return cached = Array.Empty<byte>();
                }
            }
        }
    }
}
