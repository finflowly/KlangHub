namespace KlangHub.Core.Casting
{
    /// <summary>One kind of thing a playback endpoint can tell us about itself. Provider-neutral: a
    /// Chromecast fills most of these from its mDNS announcement and its setup endpoint, another provider
    /// fills whichever it can, and the details view simply shows what arrived.</summary>
    public enum DeviceFactKind
    {
        Type,
        Model,
        Manufacturer,
        Firmware,
        CastProtocol,
        AudioFormats,
        HiResAudio,
        Multiroom,
        VolumeStep,
        Network,
        Signal,
        Address,
        MacAddress,
        Serial,
        Language,
        Uptime,
        UpdatePending,
        ReleaseTrack,
        Activity,
    }

    /// <summary>
    /// A single line of the speaker details view. <see cref="Value"/> is ready to display except for the
    /// few kinds whose value is a token the UI translates - see the provider that produced it.
    /// </summary>
    public readonly record struct DeviceFact(DeviceFactKind Kind, string Value)
    {
        /// <summary>Tokens used where the value is a concept rather than a literal, so that the wording
        /// lives in the resources and not in the provider.</summary>
        public const string Yes = "yes";
        public const string No = "no";
        public const string Ethernet = "ethernet";
        public const string TypeTelevision = "tv";
        public const string TypeSpeaker = "speaker";
        public const string TypeGroup = "group";
    }
}
