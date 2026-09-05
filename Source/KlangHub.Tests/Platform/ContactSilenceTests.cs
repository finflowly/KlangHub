using System;
using KlangHub.Communication;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// How long a device has been silent. It sounds too small to test, and it was wrong for the whole life
    /// of the code.
    /// <para>
    /// <c>(DateTime.Now - lastReceivedMessage).Seconds</c> is the seconds <i>component</i> of a difference,
    /// so it never leaves 0..59. Both places that asked the question wanted more than that - "silent for
    /// sixty seconds" and "silent for fifteen minutes" - and neither could ever be true. A device that
    /// stopped answering was therefore never given up on: its connection was closed and reopened on every
    /// poll, for ever. That is what a listener sees as a card flickering between red and green.
    /// </para>
    /// </summary>
    public class ContactSilenceTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 5, 18, 0, 0);

        [Fact]
        public void An_hour_of_silence_is_longer_than_fifteen_minutes()
        {
            // The old bug in one line: the seconds component of exactly one hour is zero.
            Assert.True(ContactSilence.LongerThan(Now.AddHours(-1), Now, TimeSpan.FromMinutes(15)));
        }

        [Fact]
        public void Ninety_seconds_of_silence_is_longer_than_a_minute()
        {
            // And here it is thirty - less than the sixty being asked about, so the guard stayed shut.
            Assert.True(ContactSilence.LongerThan(Now.AddSeconds(-90), Now, TimeSpan.FromSeconds(60)));
        }

        [Fact]
        public void A_device_that_answered_a_moment_ago_is_not_silent()
        {
            Assert.False(ContactSilence.LongerThan(Now.AddSeconds(-3), Now, TimeSpan.FromSeconds(60)));
        }

        [Fact]
        public void A_device_that_has_never_answered_is_not_called_silent()
        {
            // Nothing has been heard because nothing has been asked yet. Counting that as a timeout would
            // close the connection before the first message ever went out.
            Assert.False(ContactSilence.LongerThan(DateTime.MinValue, Now, TimeSpan.FromSeconds(60)));
        }

        [Fact]
        public void A_clock_that_jumped_backwards_does_not_invent_silence()
        {
            // Daylight saving and time synchronisation both move the clock. A negative age is not a timeout.
            Assert.False(ContactSilence.LongerThan(Now.AddMinutes(30), Now, TimeSpan.FromSeconds(60)));
        }
    }
}
