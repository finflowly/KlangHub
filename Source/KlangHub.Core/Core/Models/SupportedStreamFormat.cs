namespace KlangHub.Core.Models
{
    /// <summary>
    /// Enumeration of supported stream formats.
    /// </summary>
    public enum SupportedStreamFormat
    {
        Wav,
        Wav_16bit,
        Wav_24bit,
        Wav_32bit,
        Mp3_320,
        Mp3_128,
        // Appended last so existing persisted ordinal values (Wav=0 .. Mp3_128=5) stay stable.
        Flac
    }
}
