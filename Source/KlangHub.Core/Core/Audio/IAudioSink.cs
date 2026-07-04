namespace KlangHub.Core.Audio
{
    /// <summary>
    /// 2.2b-M2: a sink for the local capture stream. The orchestrator fans each raw-PCM <see cref="AudioFrame"/>
    /// to every active sink; each provider's sink does its own delivery — Chromecast encodes MP3/WAV and
    /// serves it over HTTP (DeliveryModel.PullHttp); AirPlay would ALAC-encode + push RTP; Snapcast would
    /// write raw PCM to a snapserver. This decouples the neutral audio path from any one provider.
    /// </summary>
    public interface IAudioSink
    {
        /// <summary>Consume one frame of captured raw PCM audio.</summary>
        void Write(AudioFrame frame);
    }
}
