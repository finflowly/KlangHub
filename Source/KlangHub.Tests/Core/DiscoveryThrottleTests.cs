using System;
using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class DiscoveryThrottleTests
    {
        private static readonly DateTime T0 = new(2026, 9, 5, 8, 8, 53, DateTimeKind.Utc);
        private const string Soundbar = "b66d62a40abc22dafc6b319c8347d54e";
        private const string Fingerprint = "192.168.8.198:8009|md=Q995GD";

        [Fact]
        public void The_six_announcements_of_one_scan_become_one_fetch()
        {
            var throttle = new DiscoveryThrottle();
            int acted = 0;
            for (int i = 0; i < 6; i++)
                if (throttle.ShouldAct(Soundbar, Fingerprint, T0.AddMilliseconds(i * 80)))
                    acted++;

            Assert.Equal(1, acted);
        }

        [Fact]
        public void A_changed_address_is_never_swallowed()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            Assert.True(throttle.ShouldAct(Soundbar, "192.168.8.204:8009|md=Q995GD", T0.AddSeconds(1)));
        }

        [Fact]
        public void A_device_never_seen_before_goes_through_at_once()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct("brand-new", Fingerprint, T0));
        }

        [Fact]
        public void An_announcement_with_no_identity_is_always_acted_on()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(null, Fingerprint, T0));
            Assert.True(throttle.ShouldAct("", Fingerprint, T0));
        }

        [Fact]
        public void The_same_device_is_refreshed_again_once_the_quiet_period_is_over()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            Assert.False(throttle.ShouldAct(Soundbar, Fingerprint, T0.AddSeconds(29)));
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0 + DiscoveryThrottle.DefaultQuietPeriod));
        }

        [Fact]
        public void Devices_do_not_throttle_each_other()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            Assert.True(throttle.ShouldAct("enchant", "192.168.8.121:8009", T0));
        }

        [Fact]
        public void Forgetting_a_device_lets_the_next_announcement_through()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            throttle.Forget(Soundbar);
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
        }
    }
}
