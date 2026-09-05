namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// What is playing right now, as far as anybody knows. Every field may be missing, and a missing field
    /// is an statement in its own right: "we do not know this yet". Nothing here is invented - a title
    /// guessed from a file name arrives with <see cref="MetadataSource.FileName"/> attached to it, so the
    /// cascade can replace it the moment something better turns up.
    /// </summary>
    public sealed record NowPlayingTrack
    {
        public string? Title { get; init; }

        public string? Artist { get; init; }

        public string? Album { get; init; }

        /// <summary>Length of the piece, when a source knows it. Not the position.</summary>
        public System.TimeSpan? Duration { get; init; }

        /// <summary>Where the file lives, when a source knows it. The key to reading its tags and its cover.</summary>
        public string? FilePath { get; init; }

        /// <summary>Codec name for the quality mark on the stage, e.g. "FLAC".</summary>
        public string? Format { get; init; }

        public int? SampleRate { get; init; }

        public int? BitDepth { get; init; }

        /// <summary>
        /// The picture found for this piece, when one was. Carried rather than a path, because it may have
        /// been lifted out of the file itself and never existed as a file of its own.
        /// </summary>
        public byte[]? CoverBytes { get; init; }

        /// <summary>True when not one field has been filled in.</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Title) &&
            string.IsNullOrWhiteSpace(Artist) &&
            string.IsNullOrWhiteSpace(Album) &&
            string.IsNullOrWhiteSpace(FilePath) &&
            Duration == null;
    }
}
