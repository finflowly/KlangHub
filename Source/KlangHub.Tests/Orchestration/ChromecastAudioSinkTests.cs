using System.Linq;
using KlangHub.Application;                 // IDevices
using KlangHub.Application.Orchestration;   // ChromecastAudioSink
using KlangHub.Core.Audio;                  // AudioFrame, AudioFormat
using KlangHub.Core.Diagnostics;            // ILogger
using KlangHub.Core.Models;                 // SupportedStreamFormat
using KlangHub.Platform.Audio;              // Mp3Encoder (reference)
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Orchestration
{
    // 2.2b-M2-3: the byte-equivalence check for the audio-sink extraction. The Chromecast encode logic was
    // moved verbatim out of the Orchestrator into ChromecastAudioSink; these assert the bytes it forwards
    // to IDevices are identical to the pre-M2 path (WAV passthrough, MP3 == a reference encoder).
    public class ChromecastAudioSinkTests
    {
        private static (ChromecastAudioSink sink, IDevices devices) NewSink()
        {
            var devices = Substitute.For<IDevices>();
            return (new ChromecastAudioSink(devices, Substitute.For<ILogger>()), devices);
        }

        [Fact]
        public void Wav_format_forwards_the_pcm_data_byte_identical()
        {
            var (sink, devices) = NewSink();
            sink.SetStreamFormat(SupportedStreamFormat.Wav);

            var pcm = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            sink.Write(new AudioFrame(pcm, 44100, 16, 2));

            devices.Received(1).OnRecordingDataAvailable(
                Arg.Is<byte[]>(d => d.SequenceEqual(pcm)),
                Arg.Any<AudioFormat>(), Arg.Any<int>(), SupportedStreamFormat.Wav);
        }

        [Fact]
        public void Mp3_format_output_is_byte_identical_to_a_reference_encoder()
        {
            var (sink, devices) = NewSink();
            sink.SetStreamFormat(SupportedStreamFormat.Mp3_320);
            byte[]? forwarded = null;
            devices.When(d => d.OnRecordingDataAvailable(Arg.Any<byte[]>(), Arg.Any<AudioFormat>(), Arg.Any<int>(), Arg.Any<SupportedStreamFormat>()))
                   .Do(ci => forwarded = ci.Arg<byte[]>());

            var pcm = new byte[176400];   // 1s @ 44.1k/16/stereo of silence -> guarantees LAME output
            var format = new AudioFormat(44100, 16, 2);

            sink.Write(new AudioFrame(pcm, 44100, 16, 2));

            byte[] reference;
            using (var enc = new Mp3Encoder(format, SupportedStreamFormat.Mp3_320, Substitute.For<ILogger>()))
            {
                enc.Encode(pcm);
                reference = enc.Read();
            }

            Assert.NotNull(forwarded);
            Assert.True(forwarded.Length > 0);
            Assert.Equal(reference, forwarded);
        }
    }
}
