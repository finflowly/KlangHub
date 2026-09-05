using System;
using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class ReconnectGateTests
    {
        private const long T0 = 4_000_000;   // a monotonic tick count, not a wall clock

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
            Assert.False(gate.TryEnter(T0 + 5_000));
        }

        [Fact]
        public void The_gate_outlasts_a_rebuild_rather_than_expiring_during_one()
        {
            // A rebuild is 2 s + stop + 2 s + connect/launch/load, around nine seconds. The 15 s status
            // poll must not slip through in the middle of it and fire a second LAUNCH.
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            Assert.False(gate.TryEnter(T0 + 9_000));
            Assert.False(gate.TryEnter(T0 + 15_000));
        }

        [Fact]
        public void Leaving_opens_it_at_once_so_a_finished_attempt_costs_nothing()
        {
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            gate.Leave();
            Assert.True(gate.TryEnter(T0 + 1));
        }

        [Fact]
        public void An_attempt_that_never_reports_back_still_times_out()
        {
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            Assert.True(gate.TryEnter(T0 + (long)ReconnectGate.DefaultQuietPeriod.TotalMilliseconds));
        }

        [Fact]
        public void The_regular_poll_is_never_blocked_once_attempts_finish()
        {
            var gate = new ReconnectGate();
            long now = T0;
            for (int tick = 0; tick < 5; tick++)
            {
                Assert.True(gate.TryEnter(now));
                gate.Leave();
                now += 15_000;
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

        [Fact]
        public void It_is_immune_to_the_clock_going_backwards()
        {
            // The end of summer time steps the wall clock back an hour. Against DateTime.Now the gate
            // latched shut for that hour and a dropped speaker never came back.
            var gate = new ReconnectGate();
            Assert.True(gate.TryEnter(T0));
            gate.Leave();

            // A monotonic count keeps rising regardless of what the calendar does.
            Assert.True(gate.TryEnter(T0 + 100));
        }
    }
}
