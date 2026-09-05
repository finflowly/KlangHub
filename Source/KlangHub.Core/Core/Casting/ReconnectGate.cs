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
    /// and the others fall away, until the attempt has had time to finish.
    /// </summary>
    public sealed class ReconnectGate
    {
        /// <summary>A rebuild waits 2 s, stops the old session, waits 2 s more and then connects. Six
        /// seconds is that sequence plus room for a slow device to answer.</summary>
        public static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromSeconds(6);

        private readonly TimeSpan quietPeriod;
        private readonly object sync = new();
        private DateTime lastEntered = DateTime.MinValue;

        public ReconnectGate(TimeSpan? quietPeriod = null)
            => this.quietPeriod = quietPeriod ?? DefaultQuietPeriod;

        /// <summary>True when the caller may start a reconnect. False means one is already under way.</summary>
        public bool TryEnter(DateTime now)
        {
            lock (sync)
            {
                if (now - lastEntered < quietPeriod)
                    return false;

                lastEntered = now;
                return true;
            }
        }

        /// <summary>Forgets the last attempt, so the next caller gets through immediately. Used when a
        /// rebuild ends early - there is no reason to keep the door shut once nobody is walking through.</summary>
        public void Reset()
        {
            lock (sync) { lastEntered = DateTime.MinValue; }
        }
    }
}
