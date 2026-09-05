using System;
using System.Threading;
using System.Threading.Tasks;
using KlangHub.Application;                 // IDevices
using KlangHub.Application.Orchestration;   // ChromecastAudioSink
using KlangHub.Core.Audio;                  // AudioFrame, AudioFormat
using KlangHub.Core.Diagnostics;            // ILogger
using KlangHub.Core.Models;                 // SupportedStreamFormat
using KlangHub.Platform.Audio;              // IAudioEncoder
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Orchestration
{
    /// <summary>
    /// An audio encoder is a state machine: FLAKE's FlakeWriter and LAME both carry a half-filled block
    /// between calls, and FLAKE reaches into its buffers through unsafe pointers. Entering one from two
    /// threads at once does not merely produce a broken stream - it writes past array bounds and corrupts
    /// the GC heap, which is how KlangHub died on 2026-09-05 with an access violation inside coreclr.
    ///
    /// The sink is the one place that can guarantee this never happens, because it owns the encoder.
    /// </summary>
    public class ChromecastAudioSinkConcurrencyTests
    {
        /// <summary>Records every time it is entered while already inside a call.</summary>
        private sealed class ReentrancyProbeEncoder : IAudioEncoder
        {
            private int inside;
            public int Overlaps;
            public int Calls;

            public void Encode(byte[] pcmBytes)
            {
                Interlocked.Increment(ref Calls);
                if (Interlocked.Exchange(ref inside, 1) == 1)
                    Interlocked.Increment(ref Overlaps);

                // Wide enough that two unsynchronised threads reliably overlap.
                Thread.Sleep(1);
                Volatile.Write(ref inside, 0);
            }

            public byte[] Read() => Array.Empty<byte>();

            public void Dispose() { }
        }

        [Fact]
        public void Write_never_enters_the_encoder_from_two_threads_at_once()
        {
            var probe = new ReentrancyProbeEncoder();
            var sink = new ChromecastAudioSink(Substitute.For<IDevices>(), Substitute.For<ILogger>())
            {
                EncoderFactory = (_, _) => probe
            };
            sink.SetStreamFormat(SupportedStreamFormat.Flac);

            var pcm = new byte[4096];
            var start = new ManualResetEventSlim();

            var writers = new Task[2];
            for (int t = 0; t < writers.Length; t++)
            {
                writers[t] = Task.Run(() =>
                {
                    start.Wait();
                    for (int i = 0; i < 50; i++)
                        sink.Write(new AudioFrame(pcm, 48000, 16, 2));
                });
            }

            start.Set();
            Task.WaitAll(writers);

            Assert.Equal(100, probe.Calls);
            Assert.Equal(0, probe.Overlaps);
        }

        [Fact]
        public void Write_creates_exactly_one_encoder_even_under_concurrent_first_calls()
        {
            var created = 0;
            var sink = new ChromecastAudioSink(Substitute.For<IDevices>(), Substitute.For<ILogger>())
            {
                EncoderFactory = (_, _) =>
                {
                    Interlocked.Increment(ref created);
                    return new ReentrancyProbeEncoder();
                }
            };
            sink.SetStreamFormat(SupportedStreamFormat.Flac);

            var pcm = new byte[4096];
            var start = new ManualResetEventSlim();
            var writers = new Task[4];
            for (int t = 0; t < writers.Length; t++)
            {
                writers[t] = Task.Run(() =>
                {
                    start.Wait();
                    sink.Write(new AudioFrame(pcm, 48000, 16, 2));
                });
            }

            start.Set();
            Task.WaitAll(writers);

            Assert.Equal(1, created);
        }
    }
}
