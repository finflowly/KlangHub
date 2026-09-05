using System;
using KlangHub.Core.Streaming;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Watching whether KlangHub is actually feeding a device fast enough.
    /// <para>
    /// Measured on 2026-09-05: two speakers each fell behind real time by two to three seconds and then
    /// dropped the connection twenty-three seconds later - audible both times. The log could not say
    /// whether KlangHub had stopped supplying data or the network had stopped carrying it, because
    /// nothing recorded how long a send took. This is that record.
    /// </para>
    /// <para>
    /// It reports only what is worth reading. A line every second for a healthy stream would bury the one
    /// second that matters under nine hundred that do not.
    /// </para>
    /// </summary>
    public class StreamHealthTests
    {
        /// <summary>FLAC of 48 kHz / 24-bit stereo, roughly what the measured runs carried.</summary>
        private const int ExpectedBytesPerSecond = 250_000;

        /// <summary>What a compressing codec reports: no figure to be measured against.</summary>
        private const int RateUnknown = 0;

        [Fact]
        public void A_healthy_second_is_not_worth_a_word()
        {
            var health = new StreamHealth(ExpectedBytesPerSecond);
            health.Sent(ExpectedBytesPerSecond, TimeSpan.FromMilliseconds(4));

            Assert.Null(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Says_so_when_a_send_blocked_for_a_long_time()
        {
            // A blocking send means the far end is not taking the data - the strongest single indication
            // that the problem is on the wire rather than in the encoder.
            var health = new StreamHealth(ExpectedBytesPerSecond);
            health.Sent(ExpectedBytesPerSecond, TimeSpan.FromMilliseconds(900));

            var report = health.Report(TimeSpan.FromSeconds(1));

            Assert.NotNull(report);
            Assert.Contains("900", report);
        }

        [Fact]
        public void Says_so_when_far_less_went_out_than_the_music_needed()
        {
            // The other half of the question: if little went out and nothing blocked, the shortage is
            // ours - the encoder did not produce it.
            var health = new StreamHealth(ExpectedBytesPerSecond);
            health.Sent(ExpectedBytesPerSecond / 2, TimeSpan.FromMilliseconds(3));

            var report = health.Report(TimeSpan.FromSeconds(1));

            Assert.NotNull(report);
            Assert.Contains("%", report);
        }

        [Fact]
        public void A_connection_that_has_never_carried_anything_says_nothing()
        {
            // The send loop runs whenever a connection is open, including while a device sits idle or
            // paused. Reporting "0 % of what the music needed" there was a complaint about silence that
            // nobody asked for - and it filled the log of an evening in which nothing was wrong.
            var health = new StreamHealth(ExpectedBytesPerSecond);

            Assert.Null(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Music_that_stops_arriving_mid_stream_is_reported()
        {
            // The same empty second means something entirely different once audio has been flowing.
            var health = new StreamHealth(ExpectedBytesPerSecond);
            health.Sent(ExpectedBytesPerSecond, TimeSpan.FromMilliseconds(4));
            health.Report(TimeSpan.FromSeconds(1));

            Assert.NotNull(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void A_stream_with_no_knowable_rate_is_not_judged_by_its_size()
        {
            // FLAC compresses by however much the music allows. Measured on 2026-09-05: four healthy
            // devices sat between seventy and eighty-five per cent of the uncompressed size for a whole
            // evening, each writing a line every second. There is no threshold that fixes that, because
            // there is no expected figure to have a threshold around.
            var health = new StreamHealth(RateUnknown);
            health.Sent(ExpectedBytesPerSecond / 3, TimeSpan.FromMilliseconds(4));

            Assert.Null(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void A_blocked_send_is_reported_even_when_the_rate_is_unknown()
        {
            // The measurement that survives compression: how long the far end made us wait.
            var health = new StreamHealth(RateUnknown);
            health.Sent(1000, TimeSpan.FromMilliseconds(900));

            var report = health.Report(TimeSpan.FromSeconds(1));

            Assert.NotNull(report);
            Assert.Contains("900", report);
            Assert.DoesNotContain("%", report);
        }

        [Fact]
        public void Music_that_stops_arriving_is_reported_even_when_the_rate_is_unknown()
        {
            var health = new StreamHealth(RateUnknown);
            health.Sent(50_000, TimeSpan.FromMilliseconds(4));
            health.Report(TimeSpan.FromSeconds(1));

            Assert.NotNull(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Small_wobbles_are_not_worth_reporting()
        {
            // Sends arrive in blocks and a second never lands exactly on the expected figure. Reporting
            // every few percent would make the log useless.
            var health = new StreamHealth(ExpectedBytesPerSecond);
            health.Sent((int)(ExpectedBytesPerSecond * 0.93), TimeSpan.FromMilliseconds(6));

            Assert.Null(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Starts_over_after_every_report()
        {
            // Each line must describe its own second, or a single bad moment would colour the rest of
            // the evening.
            var health = new StreamHealth(ExpectedBytesPerSecond);
            health.Sent(0, TimeSpan.FromMilliseconds(900));
            health.Report(TimeSpan.FromSeconds(1));

            health.Sent(ExpectedBytesPerSecond, TimeSpan.FromMilliseconds(4));

            Assert.Null(health.Report(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Names_the_longest_single_send_not_the_total()
        {
            // Twenty sends of 50 ms is a busy second; one send of 900 ms is a stall. Only the longest
            // one tells them apart.
            var health = new StreamHealth(ExpectedBytesPerSecond);
            for (var i = 0; i < 10; i++)
                health.Sent(ExpectedBytesPerSecond / 10, TimeSpan.FromMilliseconds(40));

            Assert.Null(health.Report(TimeSpan.FromSeconds(1)));
        }
    }
}
