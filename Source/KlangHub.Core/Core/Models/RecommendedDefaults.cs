namespace KlangHub.Core.Models
{
    /// <summary>
    /// What a fresh installation starts with, and what the settings page points at.
    /// <para>
    /// One place, so the value a new installation gets, the value "reset" returns to and the entry the
    /// list marks as recommended cannot drift apart. They had already begun to: the format was set in two
    /// separate places in the startup path, and the word "default" was baked into the <em>text</em> of one
    /// of the choices in all twenty-four languages - so changing the default meant changing translations,
    /// which is the surest way for a label to end up lying.
    /// </para>
    /// </summary>
    public static class RecommendedDefaults
    {
        /// <summary>
        /// FLAC. Lossless, so nothing is thrown away, and compressed, so it fits down a wireless link that
        /// uncompressed 24-bit audio can overrun - which is what the receivers were reporting as a decode
        /// error before there was anything to compress with. The depth follows whatever the capture device
        /// delivers; on hardware that captures at 24-bit, this is 24-bit end to end.
        /// </summary>
        public const SupportedStreamFormat StreamFormat = SupportedStreamFormat.Flac;

        /// <summary>
        /// Ten seconds of cushion on top of the base head-room. Enough that a speaker rejoining after a
        /// moment of interference has something to play while it catches up, and short enough that
        /// starting playback still feels immediate.
        /// </summary>
        public const int ExtraBufferSeconds = 10;

        public static bool IsRecommended(SupportedStreamFormat format) => format == StreamFormat;

        public static bool IsRecommended(int extraBufferSeconds) => extraBufferSeconds == ExtraBufferSeconds;
    }
}
