using System;
using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class ReconnectGateTests
    {
        private static readonly DateTime T0 = new(2026, 9, 5, 8, 9, 16, DateTimeKind.Utc);

        [Fact]
        public void The_first_caller_gets_through()
        {
            Assert.True(new ReconnectGate().TryEnter(T0));
        }

        [Fact]
        public void A_second_reason_arriving_at_the_same_moment_does_not_start_a_second_rebuild()
        {
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            Assert.False(gate.TryEnter(T0));
            Assert.False(gate.TryEnter(T0.AddSeconds(5)));
        }

        [Fact]
        public void Once_the_attempt_has_had_its_time_the_next_one_may_try_again()
        {
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            Assert.True(gate.TryEnter(T0 + ReconnectGate.DefaultQuietPeriod));
        }

        [Fact]
        public void The_regular_fifteen_second_poll_is_never_blocked_by_the_gate()
        {
            var gate = new ReconnectGate();
            var now = T0;
            for (int tick = 0; tick < 5; tick++)
            {
                Assert.True(gate.TryEnter(now));
                now = now.AddSeconds(15);
            }
        }

        [Fact]
        public void Reset_opens_the_door_again_immediately()
        {
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            gate.Reset();
            Assert.True(gate.TryEnter(T0));
        }
    }
}
