using System;
using KlangHub.Application;    // Device (internal ShouldRunPoll)
using KlangHub.Core.Casting;   // BackoffPolicy
using Xunit;

namespace KlangHub.Tests.Platform
{
    // Locks the ConnectError circuit-breaker: a device stuck unreachable (powered off / moved) must not hammer
    // two 5s blocking timeouts every 15s poll forever (the log showed 8+ minutes of it). ConnectError polls back
    // off 15 -> 30 -> 60s; a healthy device always polls at the normal cadence and resets the backoff.
    public class DeviceReconnectGateTests
    {
        private static BackoffPolicy NoJitter() =>
            new BackoffPolicy(baseSeconds: 15, maxSeconds: 60, jitterSeconds: 0, jitter: () => 0);

        private static readonly DateTime T0 = new DateTime(2026, 7, 5, 13, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Healthy_device_always_polls_and_keeps_backoff_reset()
        {
            var backoff = NoJitter();
            var next = T0.AddSeconds(999); // stale schedule from a prior ConnectError

            Assert.True(Device.ShouldRunPoll(false, T0, ref next, backoff));
            Assert.Equal(DateTime.MinValue, next); // reset
        }

        [Fact]
        public void ConnectError_polls_then_skips_within_the_window_then_polls_after_the_delay()
        {
            var backoff = NoJitter();
            var next = DateTime.MinValue;

            Assert.True(Device.ShouldRunPoll(true, T0, ref next, backoff));       // 1st attempt runs
            Assert.Equal(T0.AddSeconds(15), next);                                // schedules +15s

            Assert.False(Device.ShouldRunPoll(true, T0.AddSeconds(5), ref next, backoff));  // within window -> skip
            Assert.False(Device.ShouldRunPoll(true, T0.AddSeconds(14), ref next, backoff)); // still within -> skip

            var t1 = T0.AddSeconds(15);
            Assert.True(Device.ShouldRunPoll(true, t1, ref next, backoff));       // window elapsed -> run
            Assert.Equal(t1.AddSeconds(30), next);                                // backoff grew to +30s
        }

        [Fact]
        public void Recovery_resets_the_cadence_to_base()
        {
            var backoff = NoJitter();
            var next = DateTime.MinValue;

            Device.ShouldRunPoll(true, T0, ref next, backoff);                    // +15
            Device.ShouldRunPoll(true, T0.AddSeconds(15), ref next, backoff);     // +30 (now at 30 window)

            Assert.True(Device.ShouldRunPoll(false, T0.AddSeconds(50), ref next, backoff)); // recovered -> run + reset

            var t1 = T0.AddSeconds(60);
            Assert.True(Device.ShouldRunPoll(true, t1, ref next, backoff));       // back to ConnectError
            Assert.Equal(t1.AddSeconds(15), next);                                // base delay again (reset worked)
        }
    }
}
