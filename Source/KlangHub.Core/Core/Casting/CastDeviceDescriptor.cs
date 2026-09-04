namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Provider-neutral identity of a discovered playback endpoint. Just enough to identify, display
    /// and open a session; ALL provider-specific detail (Chromecast host/port/USN, a Spotify device id,
    /// ...) stays inside the provider, keyed by <see cref="Id"/>.
    /// </summary>
    /// <param name="Id">Stable, non-empty identity (Chromecast derives it from the USN). The whole
    /// re-find scheme relies on this being stable across discoveries and unique per endpoint.</param>
    /// <param name="Name">Display name.</param>
    /// <param name="Provider">Which provider this endpoint belongs to.</param>
    /// <param name="IsGroup">True for a multi-device group (e.g. a Google Cast group).</param>
    public sealed record CastDeviceDescriptor(
        string Id,
        string Name,
        ProviderId Provider,
        bool IsGroup)
    {
        /// <summary>Hardware model as announced by the endpoint (Chromecast mDNS "md=", e.g. "Google Nest
        /// Audio"), when it is known - shown as the card's subtitle. Null when the provider has no model
        /// information; never part of the identity.</summary>
        public string? Model { get; init; }

        /// <summary>Number of members behind a group, when the provider knows it (0 = unknown).</summary>
        public int MemberCount { get; init; }

        public override string ToString() => Name;
    }
}
