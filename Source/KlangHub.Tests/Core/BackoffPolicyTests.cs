using System.Linq;
using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class BackoffPolicyTests
    {
        // jitter source pinned to 0 for a deterministic sequence
        private static BackoffPolicy NoJitter() =>
            new BackoffPolicy(baseSeconds: 1.0, maxSeconds: 30.0, jitterSeconds: 0.5, jitter: () => 0.0);

        [Fact]
        public void Delays_double_from_one_second_then_cap_at_max()
        {
            var policy = NoJitter();

            var seq = Enumerable.Range(0, 7).Select(_ => policy.NextDelay().TotalSeconds).ToArray();

            Assert.Equal(new[] { 1.0, 2.0, 4.0, 8.0, 16.0, 30.0, 30.0 }, seq);
        }

        [Fact]
        public void Reset_returns_to_the_first_delay()
        {
            var policy = NoJitter();
            policy.NextDelay();
            policy.NextDelay(); // now at 4s next

            policy.Reset();

            Assert.Equal(1.0, policy.NextDelay().TotalSeconds);
        }

        [Fact]
        public void Jitter_stays_within_the_configured_band()
        {
            var high = new BackoffPolicy(1.0, 30.0, 0.5, () => 1.0);
            var low = new BackoffPolicy(1.0, 30.0, 0.5, () => -1.0);

            Assert.Equal(1.5, high.NextDelay().TotalSeconds, 3); // 1.0 + 0.5
            Assert.Equal(0.5, low.NextDelay().TotalSeconds, 3);  // 1.0 - 0.5
        }

        [Fact]
        public void Delay_never_drops_below_the_floor()
        {
            // huge negative jitter must still floor at 0.1s
            var policy = new BackoffPolicy(1.0, 30.0, 100.0, () => -1.0);

            Assert.Equal(0.1, policy.NextDelay().TotalSeconds, 3);
        }
    }
}
