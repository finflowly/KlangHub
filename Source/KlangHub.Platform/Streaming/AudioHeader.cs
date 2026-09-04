using System.IO;
using System.Text;
using KlangHub.Streaming.Interfaces;
using KlangHub.Classes;
using KlangHub.Platform.Audio;

namespace KlangHub.Streaming
{
    public class AudioHeader : IAudioHeader
    {
        /// <summary>
        /// Generate a header for a maximum length WAV stream.
        /// </summary>
        public byte[] GetRiffHeader(AudioFormat format, uint dataSize = 0)
        {
            if (format == null)
                return new byte[0];

            uint chunkSize = dataSize;
            uint factChunkSize = 4;
            uint numberOfSamples = (uint)(dataSize * 8 / format.BitsPerSample / format.Channels);

            var riffHeaderStream = new MemoryStream();
            var writer = new BinaryWriter(riffHeaderStream, Encoding.UTF8);

            writer.Write(Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(chunkSize);
            writer.Write(Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(Encoding.UTF8.GetBytes("fmt "));
            format.ToWaveFormat().Serialize(writer);   // byte-identical to the old WaveFormat.Serialize
            writer.Write(Encoding.UTF8.GetBytes("fact"));
            writer.Write(factChunkSize);
            writer.Write(numberOfSamples);
            writer.Write(Encoding.UTF8.GetBytes("data"));
            writer.Write(dataSize);

            return riffHeaderStream.ToArray();
        }

        /// <summary>
        /// The bytes that must precede the audio data for a given stream format.
        /// <para>
        /// Only WAV needs one. Raw LPCM has no structure of its own, so it gets a RIFF header describing the
        /// sample format. MP3 and FLAC are self-describing: LAME emits complete MPEG frames (each with its own
        /// 4-byte header), and the FLAC encoder writes the "fLaC" magic plus STREAMINFO before its first
        /// frame. Anything put in front of those bytes is garbage the decoder has to resynchronise past.
        /// </para>
        /// <para>
        /// This used to prepend a hand-built "MP3 header" to <b>everything that was not WAV</b> - and that
        /// header wrote each header BIT as a whole BYTE, so ~32 bytes of 0x00/0x01 landed in front of every
        /// MP3 <i>and</i> every FLAC stream. That is exactly why WAV played on the TV while FLAC and MP3
        /// stuttered or refused: the container was fine, the bytes in front of it were not.
        /// </para>
        /// </summary>
        public byte[] GetStreamHeader(AudioFormat format, SupportedStreamFormat streamFormat)
        {
            if (format == null || !StreamCodec.IsWav(streamFormat))
                return System.Array.Empty<byte>();

            return GetRiffHeader(format);
        }
    }
}
