namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Neutral volume snapshot: level, mute and the step granularity, which travel together (mirrors
    /// the Chromecast Volume DTO without leaking it). Level is 0.0 .. 1.0; StepInterval is the volume
    /// increment (0.0 .. 1.0) a UI uses for its step/tick size.
    /// </summary>
    public readonly record struct VolumeStatus(float Level, bool Muted, float StepInterval);
}
