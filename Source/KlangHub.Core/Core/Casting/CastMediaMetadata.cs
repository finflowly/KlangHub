namespace KlangHub.Core.Casting
{
    /// <summary>
    /// The metadata a cast host supplies for a LOAD: what the receiver shows on screen (title/subtitle/album +
    /// full-bleed artwork) and the codec MIME type. Kept neutral in Core so the Platform message builder and the
    /// App host both depend on it without a cross-reference.
    /// </summary>
    public sealed class CastMediaMetadata
    {
        /// <summary>Primary line shown on the receiver (e.g. the user's stream title).</summary>
        public string Title { get; set; } = "KlangHub";

        /// <summary>Secondary line — mapped to the music "artist" field (e.g. "Live from your PC").</summary>
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>Album line — the KlangHub brand.</summary>
        public string Album { get; set; } = "KlangHub";

        /// <summary>Absolute URL of the full-bleed artwork the receiver renders (served by our HTTP server).</summary>
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>Codec MIME type (see StreamCodec) — must match the streamed bytes and the HTTP header.</summary>
        public string ContentType { get; set; } = "audio/wav";
    }
}
