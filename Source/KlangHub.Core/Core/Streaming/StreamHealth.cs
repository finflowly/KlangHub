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
        /// evenly in a one-second window, so a few percent either way says nothing at all. Only ever applied
        /// to a stream whose weight is knowable - see <see cref="expectedBytesPerSecond"/>.
        /// </summary>
        private const double ShortfallThreshold = 0.85;

        /// <summary>
        /// What one second of this stream should weigh, or <c>0</c> when nobody can say.
        /// <para>
        /// FLAC compresses by however much the music allows, so there is no figure to fall short of. The
        /// first version of this class measured every stream against the <i>uncompressed</i> size and
        /// warned below 85 % of it; a healthy FLAC evening sat between 70 % and 85 % on four devices at
        /// once and wrote a line about it every second. A number nobody can predict cannot be a threshold.
        /// With no knowable weight, only the two codec-independent facts are reported: a send that blocked,
        /// and music that stopped arriving altogether.
        /// </para>
        /// </summary>
        private readonly long expectedBytesPerSecond;

        private long bytes;
        private TimeSpan longestSend;
        private int sends;

        /// <summary>
        /// Whether anything has ever gone out on this connection. An empty second means nothing while a
        /// device sits idle or paused - the send loop runs regardless - and means everything once audio has
        /// been flowing.
        /// </summary>
        private bool everCarriedAudio;

        public StreamHealth(long expectedBytesPerSecondIn)
        {
            expectedBytesPerSecond = Math.Max(0, expectedBytesPerSecondIn);
        }

        private bool WeightIsKnowable => expectedBytesPerSecond > 0;

        public void Sent(int byteCount, TimeSpan blocked)
        {
            bytes += byteCount;
            sends++;
            if (byteCount > 0)
                everCarriedAudio = true;
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

            var stalled = longest >= LongSend;
            var wentQuiet = everCarriedAudio && carried == 0;
            var expected = expectedBytesPerSecond * window.TotalSeconds;
            var short_ = WeightIsKnowable && everCarriedAudio && carried < expected * ShortfallThreshold;

            if (!stalled && !wentQuiet && !short_)
                return null;

            var howMuch = WeightIsKnowable
                ? string.Format(CultureInfo.InvariantCulture, "{0:0} % of what the music needed, ", carried / expected * 100)
                : string.Empty;

            return string.Format(CultureInfo.InvariantCulture,
                "stream health: {0}{1:0.0} KB in {2:0.0} s over {3} sends, longest single send {4:0} ms",
                howMuch, carried / 1024.0, window.TotalSeconds, count, longest.TotalMilliseconds);
        }
    }
}
