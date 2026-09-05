using System.Threading;
using System.Threading.Tasks;
using KlangHub.Core.Diagnostics;            // ILogger
using KlangHub.Platform.Audio;              // LoopbackCaptureEngine
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// The drain loop that hands captured audio to the encoder must exist exactly once per engine.
    ///
    /// It used to be started inside TryStartCapture, so every restart - Apply() when the device combo is
    /// populated, the 15-second device scan, Restart() - added another one, while the old one kept running
    /// because it only exits when isRecording is false and a restart sets that back to true within
    /// milliseconds. Two loops then drive the SAME stateful encoder from two threads, and FLAKE's unsafe
    /// pointer writes go out of bounds: the crash dump of 2026-09-05 shows two EventThread stacks inside
    /// FlacEncoder.Encode and a GC heap with 21 corrupted objects.
    ///
    /// These tests pin the invariant without touching WASAPI.
    /// </summary>
    public class LoopbackCaptureEngineEventLoopTests
    {
        private static LoopbackCaptureEngine NewEngine()
            => new LoopbackCaptureEngine(Substitute.For<ILogger>());

        [Fact]
        public void EnsureEventLoop_starts_exactly_one_loop_however_often_it_is_called()
        {
            var engine = NewEngine();
            try
            {
                for (int i = 0; i < 10; i++)
                    engine.EnsureEventLoop();

                Assert.Equal(1, WaitForLoops(engine, 1));
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public void EnsureEventLoop_starts_exactly_one_loop_when_called_from_many_threads_at_once()
        {
            var engine = NewEngine();
            try
            {
                var start = new ManualResetEventSlim();
                var callers = new Task[8];
                for (int t = 0; t < callers.Length; t++)
                {
                    callers[t] = Task.Run(() =>
                    {
                        start.Wait();
                        engine.EnsureEventLoop();
                    });
                }

                start.Set();
                Task.WaitAll(callers);

                Assert.Equal(1, WaitForLoops(engine, 1));
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public void Dispose_ends_the_event_loop()
        {
            var engine = NewEngine();
            engine.EnsureEventLoop();
            Assert.Equal(1, WaitForLoops(engine, 1));

            engine.Dispose();

            Assert.Equal(0, WaitForLoops(engine, 0));
        }

        /// <summary>Poll rather than sleep a fixed time: thread start-up is not instantaneous.</summary>
        private static int WaitForLoops(LoopbackCaptureEngine engine, int expected)
        {
            for (int i = 0; i < 200 && engine.ActiveEventLoops != expected; i++)
                Thread.Sleep(10);

            return engine.ActiveEventLoops;
        }
    }
}
