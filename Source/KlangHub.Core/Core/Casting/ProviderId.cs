namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Stable identifier of a casting provider. Replaces ad-hoc heuristics (e.g. Chromecast's
    /// "port != 8009" group check) with an explicit discriminator, so providers are first-class peers.
    /// </summary>
    public readonly record struct ProviderId(string Value)
    {
        public static readonly ProviderId Chromecast = new("chromecast");
        public static readonly ProviderId AirPlay = new("airplay");
        public static readonly ProviderId Snapcast = new("snapcast");
        public static readonly ProviderId Spotify = new("spotify");
        public static readonly ProviderId AlexaCast = new("alexa-cast");

        /// <summary>Synthetic id of a <see cref="CompositeCastProvider"/> fronting several real providers.</summary>
        public static readonly ProviderId Composite = new("composite");

        public override string ToString() => Value;
    }
}
