using KlangHub.Classes;

namespace KlangHub.Streaming.Interfaces
{
    public interface IAudioHeader
    {
        byte[] GetRiffHeader(AudioFormat format, uint dataSize = 0);
        byte[] GetStreamHeader(AudioFormat format, SupportedStreamFormat streamFormat);
    }
}