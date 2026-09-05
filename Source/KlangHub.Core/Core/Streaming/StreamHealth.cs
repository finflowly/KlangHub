using System;
using System.Globalization;

namespace KlangHub.Core.Streaming
{
    /// <summary>
    /// Watches whether a device is actually being fed fast enough, and says so only when it is not.
    /// <para>
    /// Why it exists: on 2026-09-05 two speakers each fell two to three seconds behind real time and then
    /// dropped the connection twenty-three seconds later, audibly, twice. The log could not settle whether
    /// KlangHub had stopped supplying audio or the network had stopped carrying it - nothing recorded how
    /// long a send took. Without that one number, any change to buffering would have been guesswork.
    /// </para>
    /// <para>
    /// The two numbers answer the question between them. A send that <b>blocked</b> for a long time means
    /// the far end stopped taking data: the trouble is on the wire. Little sent with <b>no</b> blocking
    /// means the encoder did not produce it: the trouble is ours.
    /// </para>
    /// </summary>
    public sealed class StreamHealth
    {
        /// <summary>
        /// A send that takes this long is not a busy moment, it is a stall. Sends normally return in
        /// single-digit milliseconds because the operating system's own buffer swallows them.
        /// </summary>
        private static readonly TimeSpan LongSend = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Below this share of what the music needs, the stream is genuinely short. Blocks do not land
        /// evenly in a one-second window, so a few percent either way says nothing at all.
        /// </summary>
        private const double ShortfallThreshold = 0.85;

        private readonly long expectedBytesPerSecond;
        private long bytes;
        private TimeSpan longestSend;
        private int sends;

        public StreamHealth(long expectedBytesPerSecondIn)
        {
            expectedBytesPerSecond = Math.Max(1, expectedBytesPerSecondIn);
        }

        public void Sent(int byteCount, TimeSpan blocked)
        {
            bytes += byteCount;
            sends++;
            if (blocked > longestSend)
                longestSend = blocked;
        }

        /// <summary>
        /// One line about the window that just ended, or null when there is nothing worth saying. Always
        /// starts the next window, whether it reported or not.
        /// </summary>
        public string? Report(TimeSpan window)
        {
            var carried = bytes;
            var longest = longestSend;
            var count = sends;

            bytes = 0;
            longestSend = TimeSpan.Zero;
            sends = 0;

            if (window <= TimeSpan.Zero)
                return null;

            var expected = expectedBytesPerSecond * window.TotalSeconds;
            var share = expected > 0 ? carried / expected : 1;

            var stalled = longest >= LongSend;
            var short_ = share < ShortfallThreshold;

            if (!stalled && !short_)
                return null;

            return string.Format(CultureInfo.InvariantCulture,
                "stream health: {0:0} % of what the music needed ({1:0.0} KB in {2:0.0} s over {3} sends), longest single send {4:0} ms",
                share * 100, carried / 1024.0, window.TotalSeconds, count, longest.TotalMilliseconds);
        }
    }
}
