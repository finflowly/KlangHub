using System;
using System.IO;
using System.Reflection;

namespace KlangHub.Classes
{
    /// <summary>
    /// Hands the streaming server the branded now-playing picture the Chromecast shows full-screen.
    /// <para>
    /// The picture is drawn at runtime in the user's language (<see cref="ArtworkRenderer"/>) - a fixed PNG
    /// would put an English claim on the television of someone running the app in Greek. The PNG embedded in
    /// the assembly remains the fallback when drawing is unavailable.
    /// </para>
    /// </summary>
    public static class ArtworkImage
    {
        private const string ResourceName = "KlangHub.Resources.artwork.png";
        private static byte[]? cached;

        /// <summary>The artwork to serve: rendered for the current language, else the embedded PNG.</summary>
        public static byte[] Bytes => ArtworkRenderer.CurrentPng();

        /// <summary>The PNG shipped inside the assembly, or an empty array if missing (never throws).</summary>
        public static byte[] EmbeddedBytes
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
