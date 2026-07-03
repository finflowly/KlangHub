namespace KlangHub.Core.Casting
{
    /// <summary>
    /// Neutral volume snapshot: level plus mute, which always travel together (mirrors the
    /// Chromecast Volume DTO without leaking it). Level is 0.0 .. 1.0.
    /// </summary>
    public readonly record struct VolumeStatus(float Level, bool Muted);
}
