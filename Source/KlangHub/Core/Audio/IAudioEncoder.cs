using System;

namespace KlangHub.Core.Audio
{
    /// <summary>
    /// Encodes raw WAV audio into a target stream format (e.g. MP3).
    /// Deliberately UI- and NAudio-agnostic: the whole surface is plain bytes,
    /// so this contract can live in the provider-neutral Core layer.
    /// </summary>
    public interface IAudioEncoder : IDisposable
    {
        /// <summary>Feed raw WAV bytes into the encoder.</summary>
        void Encode(byte[] wav);

        /// <summary>Read the bytes encoded so far (may be empty).</summary>
        byte[] Read();
    }
}
