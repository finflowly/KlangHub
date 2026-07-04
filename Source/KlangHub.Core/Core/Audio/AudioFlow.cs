namespace KlangHub.Core.Audio
{
    /// <summary>
    /// Direction of an audio endpoint. Neutral replacement for NAudio's
    /// <c>NAudio.CoreAudioApi.DataFlow</c> so the Core layer stays NAudio-free.
    /// </summary>
    public enum AudioFlow
    {
        Render,
        Capture,
        All
    }
}
