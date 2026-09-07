using System;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>What a contribution did to what we know.</summary>
    /// <param name="Changed">Something on the stage would look different now.</param>
    /// <param name="IsNewTrack">A different piece is playing - the scene may cut.</param>
    public readonly record struct CascadeResult(bool Changed, bool IsNewTrack);

    /// <summary>
    /// Merges what the sources report into the single best answer to "what is playing".
    /// <para>
    /// It knows no file, no WinRT and no clock: contributions go in, the best available truth comes out.
    /// That is what makes the rules in here testable, and the rules are the product - a listener casting
    /// from Clementine sees a title at all only because these five decisions are made correctly.
    /// </para>
    /// </summary>
    public sealed class NowPlayingCascade
    {
        private FieldValue<string> title;
        private FieldValue<string> artist;
        private FieldValue<string> album;
        private FieldValue<string> filePath;
        private FieldValue<string> format;
        private FieldValue<TimeSpan?> duration;
        private FieldValue<int?> sampleRate;
        private FieldValue<int?> bitDepth;

        /// <summary>The best answer anyone has so far.</summary>
        public NowPlayingTrack Current { get; private set; } = new();

        /// <summary>Raised when the merged result actually changed. Never for a repeated poll.</summary>
        public event EventHandler<NowPlayingTrack>? Changed;

        public CascadeResult Contribute(MetadataSource source, NowPlayingTrack contribution)
        {
            if (contribution == null || contribution.IsEmpty)
                return new CascadeResult(false, false);

            var isNewTrack = DecideNewTrack(source, contribution);
            if (isNewTrack)
                Forget();

            Accept(ref title, source, contribution.Title);
            AcceptArtist(ref artist, source, contribution.Artist, title, contribution.Title);
            Accept(ref album, source, contribution.Album);
            Accept(ref filePath, source, contribution.FilePath);
            Accept(ref format, source, contribution.Format);
            AcceptValue(ref duration, source, contribution.Duration);
            AcceptValue(ref sampleRate, source, contribution.SampleRate);
            AcceptValue(ref bitDepth, source, contribution.BitDepth);

            var merged = new NowPlayingTrack
            {
                Title = title.Value,
                Artist = artist.Value,
                Album = album.Value,
                FilePath = filePath.Value,
                Format = format.Value,
                Duration = duration.Value,
                SampleRate = sampleRate.Value,
                BitDepth = bitDepth.Value
            };

            var changed = merged != Current;
            Current = merged;
            if (changed)
                Changed?.Invoke(this, merged);

            return new CascadeResult(changed, isNewTrack);
        }

        /// <summary>
        /// Who is allowed to say "a different piece is playing".
        /// <para>
        /// Every living source may: they are watching the player. A name guessed from a file may not - it
        /// is a derivation, and letting it claim a track change is how a tidy "Teardrop" read from the tags
        /// gets thrown away and replaced by "03 Teardrop" off the disc. A guess may still announce a change
        /// through the one hard fact it carries: a different file is a different recording.
        /// </para>
        /// </summary>
        private bool DecideNewTrack(MetadataSource source, NowPlayingTrack contribution)
        {
            if (Current.IsEmpty)
                return true;

            if (source != MetadataSource.FileName)
                return TrackChange.IsNewTrack(Current, contribution);

            return TrackChange.IsNewTrack(
                new NowPlayingTrack { FilePath = filePath.Value },
                new NowPlayingTrack { FilePath = contribution.FilePath });
        }

        /// <summary>Rule 5: a new piece starts from nothing, or the last album hangs off this one.</summary>
        private void Forget()
        {
            title = default;
            artist = default;
            album = default;
            filePath = default;
            format = default;
            duration = default;
            sampleRate = default;
            bitDepth = default;
        }

        private static void AcceptArtist(ref FieldValue<string> field, MetadataSource source, string? value,
                                         FieldValue<string> heldTitle, string? offeredTitle)
        {
            if (source < heldTitle.Source &&
                field.Source == MetadataSource.None &&
                !string.IsNullOrWhiteSpace(offeredTitle) &&
                !string.Equals(offeredTitle.Trim(), (heldTitle.Value ?? string.Empty).Trim(),
                               StringComparison.OrdinalIgnoreCase))
                return;

            Accept(ref field, source, value);
        }

        /// <summary>
        /// Rules 2, 3 and 4 in one place: a blank says nothing and never wins (3); otherwise the value is
        /// taken when its source is at least as trustworthy as the one that answered before, which lets a
        /// better source replace a weaker one (2) and lets a source correct itself (4).
        /// </summary>
        private static void Accept(ref FieldValue<string> field, MetadataSource source, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (source >= field.Source)
                field = new FieldValue<string>(value.Trim(), source);
        }

        private static void AcceptValue<T>(ref FieldValue<T?> field, MetadataSource source, T? value)
            where T : struct
        {
            if (value == null)
                return;

            if (source >= field.Source)
                field = new FieldValue<T?>(value, source);
        }

        /// <summary>
        /// A value together with where it came from. Carrying the origin is what makes the cascade possible
        /// at all - without it there is no way to tell an improvement from a regression.
        /// </summary>
        private readonly record struct FieldValue<T>(T? Value, MetadataSource Source);
    }
}
