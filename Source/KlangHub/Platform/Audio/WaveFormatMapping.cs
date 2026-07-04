using NAudio.Wave;
using KlangHub.Core.Audio;

namespace KlangHub.Platform.Audio
{
    /// <summary>
    /// 2.2b-H2: maps the neutral Core <see cref="AudioFormat"/> to/from NAudio's <c>WaveFormat</c>.
    /// Lives in the Platform audio layer so only Platform code touches NAudio; the neutral audio
    /// pipeline (device + streaming contracts) carries <see cref="AudioFormat"/>.
    /// </summary>
    public static class WaveFormatMapping
    {
        public static WaveFormat ToWaveFormat(this AudioFormat format)
            => new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);

        public static AudioFormat ToAudioFormat(this WaveFormat format)
            => new AudioFormat(format.SampleRate, format.BitsPerSample, format.Channels);
    }
}
