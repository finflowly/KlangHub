using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUETools.Codecs;
using CUETools.Codecs.FLAKE;
using KlangHub.Core.Audio;
using KlangHub.Core.Diagnostics;
using KlangHub.Platform.Audio;
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class FlacEncoderTests
    {
        private static readonly byte[] FlacMagic = { 0x66, 0x4C, 0x61, 0x43 }; // "fLaC"

        private static byte[] Pcm16(int channels, int frames, Func<int, short> gen)
        {
            var bytes = new byte[frames * channels * 2];
            int p = 0;
            for (int i = 0; i < frames; i++)
            {
                short s = gen(i);
                for (int c = 0; c < channels; c++)
                {
                    bytes[p++] = (byte)(s & 0xFF);
                    bytes[p++] = (byte)((s >> 8) & 0xFF);
                }
            }
            return bytes;
        }

        [Fact]
        public void Encode_emits_a_flac_stream_starting_with_the_fLaC_magic()
        {
            var logger = Substitute.For<ILogger>();
            var enc = new FlacEncoder(new AudioFormat(44100, 16, 2), logger, compressionLevel: 5);

            // ~0.5 s of a quiet ramp so at least one FLAC frame is emitted.
            enc.Encode(Pcm16(2, 22050, i => (short)(i % 2000 - 1000)));
            enc.Dispose(); // flush the trailing partial block

            var outBytes = enc.Read();

            Assert.True(outBytes.Length >= 4, "expected FLAC output bytes");
            Assert.Equal(FlacMagic, outBytes.Take(4).ToArray());
        }

        [Fact]
        public void Encode_of_silence_compresses_far_below_raw_pcm()
        {
            var logger = Substitute.For<ILogger>();
            var enc = new FlacEncoder(new AudioFormat(48000, 16, 2), logger);

            var pcm = new byte[48000 * 2 * 2]; // 1 s of 16-bit stereo silence
            enc.Encode(pcm);
            enc.Dispose();

            var flac = enc.Read();

            Assert.Equal(FlacMagic, flac.Take(4).ToArray());
            // Silence must FLAC-compress to a small fraction of the raw PCM — proves real framing, not passthrough.
            Assert.True(flac.Length > 0 && flac.Length < pcm.Length / 4,
                $"expected real FLAC compression, got {flac.Length} bytes from {pcm.Length} PCM bytes");
        }

        [Fact]
        public void Encoded_flac_decodes_losslessly_back_to_the_original_pcm()
        {
            var logger = Substitute.For<ILogger>();
            var format = new AudioFormat(44100, 16, 2);
            const int frames = 44100; // 1 s

            // deterministic sawtooth so the check is meaningful (non-silent, non-constant)
            var pcm = Pcm16(2, frames, i => (short)(((i * 7) % 4000) - 2000));

            byte[] flac;
            using (var enc = new FlacEncoder(format, logger, compressionLevel: 5))
            {
                enc.Encode(pcm);
                enc.Dispose();       // flush the trailing block
                flac = enc.Read();
            }

            // The live stream carries STREAMINFO total_samples = 0 (unknown length) — correct for an endless
            // stream, and how a streaming decoder (Chromecast's Shaka/CAF, which decodes frames as they arrive)
            // consumes it. FLAKE's *file* reader instead trusts that count and would stop at 0, so we patch in
            // the real sample count (the 32-bit field at byte offset 22 — exactly where FlakeWriter itself
            // back-patches it) before decoding, to prove the encoded FRAME DATA is genuinely lossless.
            flac[22] = (byte)((frames >> 24) & 0xFF);
            flac[23] = (byte)((frames >> 16) & 0xFF);
            flac[24] = (byte)((frames >> 8) & 0xFF);
            flac[25] = (byte)(frames & 0xFF);

            var decoded = new List<byte>(pcm.Length);
            using (var ms = new MemoryStream(flac))
            {
                var reader = new FlakeReader(null, ms);
                var buff = new AudioBuffer(new AudioPCMConfig(16, 2, 44100), 0x10000);
                while (reader.Read(buff, -1) > 0)
                    decoded.AddRange(buff.Bytes.Take(buff.ByteLength));
            }

            Assert.Equal(pcm.Length, decoded.Count);
            Assert.Equal(pcm, decoded.ToArray()); // lossless: identical samples out
        }
    }
}
