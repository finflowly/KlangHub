namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Neutral volume snapshot: level, mute and the step granularity, which travel together (mirrors
    /// the Chromecast Volume DTO without leaking it). Level is 0.0 .. 1.0; StepInterval is the volume
    /// increment (0.0 .. 1.0) a UI uses for its step/tick size.
    /// </summary>
    /// <param name="IsFixed">
    /// The device owns its own volume and will not take a new one - a television on a fixed line output,
    /// a speaker behind an amplifier. It is carried here because a caller that does not know this will
    /// send a level, see the old one come back, and send it again for as long as the device is connected.
    /// Defaulted so that the many places building a status for an ordinary speaker did not all have to
    /// change.
    /// </param>
    public readonly record struct VolumeStatus(float Level, bool Muted, float StepInterval, bool IsFixed = false);
}
