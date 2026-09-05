using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Lets one reconnect attempt through at a time.
    ///
    /// A lost speaker can be noticed from several places at once - the audio socket failing, the status
    /// poll finding the device in an error state, a CLOSE message arriving late. Each of them is a correct
    /// reason to rebuild the session, but two rebuilds running at the same time would send two LAUNCH and
    /// two LOAD sequences to the same device and leave one of them orphaned. So the first one through wins
    /// and the others fall away, until the attempt has had time to finish - or says it is done.
    ///
    /// It is a latch with a safety timeout, not a timer. <see cref="Leave"/> is what normally opens it
    /// again; the timeout only covers an attempt that never reports back, and is therefore set to comfortably
    /// more than a rebuild takes rather than to a guess at its duration.
    /// </summary>
    public sealed class ReconnectGate
    {
        /// <summary>
        /// How long a silent attempt blocks the next one. A rebuild waits 2 s, stops the old session, waits
        /// 2 s more, then connects, launches and loads - around nine seconds, and longer on a slow device.
        /// Six seconds was less than that, so the status poll sailed through the open gate and issued the
        /// second LAUNCH this class exists to prevent. Twenty is past any rebuild and still well under the
        /// fifteen-second poll's second visit.
        /// </summary>
        public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromSeconds(20);

        private readonly TimeSpan quietPeriod;
        private readonly object sync = new();

        /// <summary>
        /// Monotonic, from <see cref="Environment.TickCount64"/>. Wall-clock time steps backwards - at the
        /// end of summer time by a whole hour - and a gate compared against it would have latched shut for
        /// the length of the jump, so a speaker that dropped during that hour would never have come back.
        /// </summary>
        private long enteredAtMs = long.MinValue;
        private bool occupied;

        public ReconnectGate(TimeSpan? quietPeriod = null)
            => this.quietPeriod = quietPeriod ?? DefaultQuietPeriod;

        /// <summary>True when the caller may start a reconnect. False means one is already under way.</summary>
        public bool TryEnter() => TryEnter(Environment.TickCount64);

        /// <summary>Testable overload: <paramref name="nowMs"/> is a monotonic millisecond count.</summary>
        public bool TryEnter(long nowMs)
        {
            lock (sync)
            {
                if (occupied && nowMs - enteredAtMs < (long)quietPeriod.TotalMilliseconds)
                    return false;

                occupied = true;
                enteredAtMs = nowMs;
                return true;
            }
        }

        /// <summary>
        /// The attempt is over - successful or not - so the next reason to reconnect may act at once.
        ///
        /// Without this the gate was a fixed wait: a rebuild that gave up early (a cancelled task, a device
        /// no longer in a state to be resumed) still held the door shut, and the fast-recovery paths that
        /// exist to remove exactly that delay were turned away for the remaining seconds.
        /// </summary>
        public void Leave()
        {
            lock (sync) { occupied = false; }
        }

        /// <summary>Forgets the last attempt entirely. Same effect as <see cref="Leave"/>; kept for callers
        /// that mean "start over" rather than "that attempt has finished".</summary>
        public void Reset() => Leave();
    }
}
