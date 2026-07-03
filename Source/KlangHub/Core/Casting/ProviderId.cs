namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Stable identifier of a casting provider. Replaces ad-hoc heuristics (e.g. Chromecast's
    /// "port != 8009" group check) with an explicit discriminator, so providers are first-class peers.
    /// </summary>
    public readonly record struct ProviderId(string Value)
    {
        public static readonly ProviderId Chromecast = new("chromecast");
        public static readonly ProviderId Spotify = new("spotify");
        public static readonly ProviderId AlexaCast = new("alexa-cast");

        public override string ToString() => Value;
    }
}
