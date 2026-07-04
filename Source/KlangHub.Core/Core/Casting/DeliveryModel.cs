namespace KlangHub.Core.Casting
{
    /// <summary>
    /// How a provider gets KlangHub's local capture audio to its endpoint - the bridge to the audio-sink
    /// seam. Chromecast serves an HTTP stream the device pulls; AirPlay pushes encrypted RTP to the
    /// receiver; Snapcast feeds one PCM stream into a snapserver that fans it out to synced clients.
    /// </summary>
    public enum DeliveryModel
    {
        /// <summary>App serves an HTTP stream; the device connects and pulls it (Chromecast).</summary>
        PullHttp,

        /// <summary>App pushes an (encoded, encrypted) RTP stream to the receiver (AirPlay).</summary>
        PushRtp,

        /// <summary>App feeds one PCM stream into a server that distributes to synced clients (Snapcast).</summary>
        ServerFed,
    }
}
