namespace KlangHub.Core.Audio
{
    /// <summary>
    /// Neutral PCM WAV format descriptor - the fields NAudio's <c>WaveFormat</c> carries - so the
    /// Core/domain contracts can express an audio format without touching NAudio. The Platform layer
    /// maps to/from <c>WaveFormat</c> at its boundary (see <see cref="AudioFrame"/>'s note).
    /// </summary>
    public sealed record AudioFormat(int SampleRate, int BitsPerSample, int Channels)
    {
        /// <summary>Bytes per sample frame (all channels): Channels * BitsPerSample/8.</summary>
        public int BlockAlign => Channels * (BitsPerSample / 8);

        /// <summary>Bytes per second: SampleRate * BlockAlign.</summary>
        public int AverageBytesPerSecond => SampleRate * BlockAlign;
    }
}
