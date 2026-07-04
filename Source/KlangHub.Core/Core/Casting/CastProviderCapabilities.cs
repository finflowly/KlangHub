namespace KlangHub.Core.Casting
{
    /// <summary>
    /// What a provider can do and how it sources audio. <see cref="ConsumesLocalCapture"/> is the
    /// bridge to the audio seam: <c>true</c> means the provider plays KlangHub's local capture stream
    /// (Chromecast); <c>false</c> means it is cloud-sourced and ignores local capture (Spotify Connect).
    /// </summary>
    /// <param name="DeliveryModel">How the provider gets local capture to its endpoint (2.2b-M2).</param>
    /// <param name="RequiresPairing">True if opening a session needs a pairing/auth step (e.g. AirPlay 2).</param>
    public sealed record CastProviderCapabilities(
        bool ConsumesLocalCapture,
        bool SupportsGrouping,
        bool SupportsVolumeControl,
        DeliveryModel DeliveryModel = DeliveryModel.PullHttp,
        bool RequiresPairing = false);
}
