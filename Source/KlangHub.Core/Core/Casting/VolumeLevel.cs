using System;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// The arithmetic behind a volume change, kept away from the device so it can be reasoned about.
    /// <para>
    /// Every part of this exists because of one fault seen on real hardware: a device reporting
    /// <c>0.13999999</c> where <c>0.14</c> was asked for, and being sent another SET_VOLUME for it, over
    /// and over. Those digits were never the device's. The level had been built by adding
    /// <c>stepInterval</c> to itself in a loop until it passed the target, and seven additions of 0.02
    /// land exactly there. It was then compared to the clean target with <c>!=</c> on a float - a
    /// comparison that, given how the number was made, could only ever disagree.
    /// </para>
    /// <para>
    /// The one tolerance that did exist was <c>stepInterval</c> itself, and a device whose volume cannot
    /// be changed reports a step of 0. So the tolerance was zero for exactly the devices that could never
    /// agree in the first place.
    /// </para>
    /// </summary>
    public static class VolumeLevel
    {
        /// <summary>
        /// Half a percent. Cast levels run 0..1 and no device resolves finer than a percent, so anything
        /// below this is arithmetic, not a difference somebody could hear or asked for.
        /// </summary>
        public const float Tolerance = 0.005f;

        /// <summary>
        /// Whether the device refuses volume changes altogether - a television on a fixed line output, a
        /// speaker wired to an amplifier that owns the volume. It reports its level and expects to be
        /// left alone; sending SET_VOLUME to it can never succeed and will be retried forever by anything
        /// that waits for the level to change.
        /// <para>
        /// Until this was written, <c>controlType</c> was deserialised and then never read anywhere in
        /// the project.
        /// </para>
        /// </summary>
        public static bool IsFixed(string? controlType)
            => string.Equals(controlType, "fixed", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether two levels are the same level, allowing for how floats are made.</summary>
        public static bool Same(float a, float b)
            => MathF.Abs(a - b) < Tolerance;

        /// <summary>
        /// The nearest level the device actually accepts. Rounded in one operation rather than reached by
        /// repeated addition, which is what accumulated the error this class is named after.
        /// </summary>
        /// <param name="level">the level asked for, 0..1</param>
        /// <param name="stepInterval">the device's own step; 0 or less means it did not say, so the level
        /// is passed through unsnapped</param>
        public static float Quantise(float level, float stepInterval)
        {
            if (float.IsNaN(level))
                return 0f;

            if (stepInterval > 0f && float.IsFinite(stepInterval))
                level = MathF.Round(level / stepInterval) * stepInterval;

            return Math.Clamp(level, 0f, 1f);
        }

        /// <summary>
        /// How many times a speaker is asked to come back below its own hard cap before its answer is
        /// accepted. Some devices clamp to a maximum of their own and will not go where they are told.
        /// </summary>
        public const int MaxCapAttempts = 3;

        /// <summary>
        /// Whether a speaker reporting a level above the maximum its owner set for it should be pushed
        /// back down.
        /// <para>
        /// This ran on every incoming status message with no condition beyond "is it too loud", which is
        /// what turned a device that could not comply into an argument without end: a fixed-volume
        /// speaker reports the same level for ever, and each report produced another SET_VOLUME.
        /// </para>
        /// </summary>
        /// <param name="reportedPercent">the level the device says it is at, 0..100</param>
        /// <param name="capPercent">the maximum its owner allowed it, 0..100</param>
        /// <param name="isFixed">whether the device can change its volume at all</param>
        /// <param name="attemptsSoFar">how many times it has already been asked, since it was last inside the cap</param>
        public static bool ShouldPushDownToCap(int reportedPercent, int capPercent, bool isFixed, int attemptsSoFar)
            => !isFixed
               && reportedPercent > capPercent
               && attemptsSoFar < MaxCapAttempts;
    }
}
