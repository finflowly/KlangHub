using System;

namespace KlangHub.Communication
{
    /// <summary>
    /// How long a device has gone without saying anything.
    /// <para>
    /// This lived inside <c>DeviceCommunication</c> as one line, and that line was wrong:
    /// <c>(DateTime.Now - lastReceivedMessage).Seconds</c> reads the seconds <i>component</i> of a
    /// difference, which never leaves 0..59. Both callers asked about longer spans - sixty seconds before
    /// abandoning a send, fifteen minutes before stopping waiting for a status reply - and so neither guard
    /// could ever fire. A device that had gone away was never given up on; its connection was closed and
    /// reopened on every poll, for as long as the application ran.
    /// </para>
    /// <para>
    /// It is a static function of three values so that the arithmetic can be stated as a fact and checked
    /// without a device, a socket or a clock.
    /// </para>
    /// </summary>
    internal static class ContactSilence
    {
        /// <summary>
        /// True when the last message arrived longer ago than <paramref name="limit"/>.
        /// <para>
        /// A device that has never answered is not silent - nothing has been asked of it yet - and a clock
        /// that has moved backwards (daylight saving, a time synchronisation) does not create silence
        /// either.
        /// </para>
        /// </summary>
        public static bool LongerThan(DateTime lastReceived, DateTime now, TimeSpan limit)
        {
            if (lastReceived == DateTime.MinValue)
                return false;

            return now - lastReceived > limit;
        }
    }
}
