namespace KlangHub.Core.Audio
{
    /// <summary>
    /// A chunk of captured audio plus the WAV format that describes it, expressed with plain
    /// primitives so the Core layer never touches NAudio's <c>WaveFormat</c>. The Platform layer
    /// converts to/from <c>WaveFormat</c> at its boundary.
    /// </summary>
    public sealed record AudioFrame(
        byte[] Data,
        int SampleRate,
        int BitsPerSample,
        int Channels);
}
