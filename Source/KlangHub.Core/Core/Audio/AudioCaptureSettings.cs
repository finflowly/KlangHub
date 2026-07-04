namespace KlangHub.Core.Audio
{
    /// <summary>
    /// Capture configuration supplied by the UI. Carries no UI types: the engine is told what to do,
    /// it never reaches back into the UI to pull this state.
    /// </summary>
    /// <param name="DeviceId">The endpoint to capture, or null/empty to let the engine pick a working default.</param>
    /// <param name="StreamFormat">The desired output stream format.</param>
    /// <param name="ConvertMultiChannelToStereo">Down-mix multi-channel input to stereo.</param>
    public sealed record AudioCaptureSettings(
        string DeviceId,
        SupportedStreamFormat StreamFormat,
        bool ConvertMultiChannelToStereo);
}
