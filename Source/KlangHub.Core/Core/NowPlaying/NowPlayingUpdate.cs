namespace KlangHub.Core.NowPlaying
{
    public readonly record struct NowPlayingUpdate(NowPlayingTrack Track, bool IsNewTrack);
}
