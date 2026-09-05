using KlangHub.Application;
using KlangHub.Core.Casting;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class DiscoveryThrottleTests
    {
        private const long T0 = 4_000_000;   // a monotonic tick count, not a wall clock
        private const string Soundbar = "b66d62a40abc22dafc6b319c8347d54e";
        private const string Fingerprint = "192.168.8.198:8009|md=Q995GD";

        [Fact]
        public void The_six_announcements_of_one_scan_become_one_fetch()
        {
            var throttle = new DiscoveryThrottle();
            int acted = 0;
            for (int i = 0; i < 6; i++)
                if (throttle.ShouldAct(Soundbar, Fingerprint, T0 + i * 80))
                    acted++;

            Assert.Equal(1, acted);
        }

        [Fact]
        public void A_changed_address_is_never_swallowed()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            Assert.True(throttle.ShouldAct(Soundbar, "192.168.8.204:8009|md=Q995GD", T0 + 1_000));
        }

        [Fact]
        public void A_device_never_seen_before_goes_through_at_once()
        {
            Assert.True(new DiscoveryThrottle().ShouldAct("brand-new", Fingerprint, T0));
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
            Assert.False(throttle.ShouldAct(Soundbar, Fingerprint, T0 + 29_000));
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0 + (long)DiscoveryThrottle.DefaultQuietPeriod.TotalMilliseconds));
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

        [Fact]
        public void Scan_again_forgets_every_device_so_the_button_is_not_a_no_op()
        {
            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            Assert.True(throttle.ShouldAct("enchant", "x", T0));

            throttle.ForgetAll();

            Assert.True(throttle.ShouldAct(Soundbar, Fingerprint, T0));
            Assert.True(throttle.ShouldAct("enchant", "x", T0));
        }

        [Fact]
        public void A_casting_device_is_throttled_just_like_an_idle_one()
        {
            // The TXT record's "rs" and "st" change the moment we cast to a speaker. Comparing the whole
            // record made every announcement look new for exactly the devices that were busy decoding
            // audio - the ones this class exists to leave alone.
            const string idle = "id=b66d;cd=X;rm=;ve=05;md=Q995GD;fn=Soundbar;ca=199172;st=0;rs=";
            const string casting = "id=b66d;cd=X;rm=;ve=05;md=Q995GD;fn=Soundbar;ca=199172;st=1;rs=Casting: KlangHub";

            Assert.Equal(CastTxt.IdentityFingerprint(idle), CastTxt.IdentityFingerprint(casting));

            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, CastTxt.IdentityFingerprint(idle), T0));
            Assert.False(throttle.ShouldAct(Soundbar, CastTxt.IdentityFingerprint(casting), T0 + 100));
        }

        [Fact]
        public void A_device_that_was_renamed_or_moved_still_comes_through_at_once()
        {
            const string before = "id=b66d;ve=05;md=Q995GD;fn=Soundbar;ca=199172";
            const string renamed = "id=b66d;ve=05;md=Q995GD;fn=Wohnzimmer;ca=199172";

            var throttle = new DiscoveryThrottle();
            Assert.True(throttle.ShouldAct(Soundbar, CastTxt.IdentityFingerprint(before), T0));
            Assert.True(throttle.ShouldAct(Soundbar, CastTxt.IdentityFingerprint(renamed), T0 + 100));
        }
    }
}
