namespace KlangHub.Core.Audio
{
    /// <summary>
    /// Provider-neutral description of an audio capture/render endpoint.
    /// Neutral replacement for the NAudio-coupled <c>RecordingDevice</c>; carries no NAudio types.
    /// </summary>
    public sealed record AudioCaptureDevice(
        string Id,
        string Name,
        AudioFlow Flow,
        int SampleRate,
        int Channels)
    {
        /// <summary>Display name (used e.g. as a combo-box item caption).</summary>
        public override string ToString() => Name;
    }
}
