namespace KlangHub.Core.Casting
{
    /// <summary>
    /// What a provider can do and how it sources audio. <see cref="ConsumesLocalCapture"/> is the
    /// bridge to the audio seam: <c>true</c> means the provider plays KlangHub's local capture stream
    /// (Chromecast); <c>false</c> means it is cloud-sourced and ignores local capture (Spotify Connect).
    /// </summary>
    public sealed record CastProviderCapabilities(
        bool ConsumesLocalCapture,
        bool SupportsGrouping,
        bool SupportsVolumeControl);
}
