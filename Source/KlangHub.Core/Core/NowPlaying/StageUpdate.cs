using System;
using System.Collections.Generic;
using System.Globalization;

namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// One message to the stage on the television.
    /// <para>
    /// This is the live channel that makes a track change possible without touching the audio. A fresh LOAD
    /// would work too, and it is what a naive implementation reaches for - but on a loopback stream a LOAD
    /// tears the connection down and refills the buffer, which is one to three seconds of silence on every
    /// single track. The picture has to be able to change while the music does not.
    /// </para>
    /// Two rules run through it. A field that is not known is <c>null</c>, never an empty string: null means
    /// "no news", and the stage keeps what it already had. And no technical claim is made that the file did
    /// not actually support - an invented quality mark is worse than none.
    /// </summary>
    public sealed record StageUpdate
    {
        public string? Title { get; init; }

        public string? Artist { get; init; }

        public string? Album { get; init; }

        public string? Zone { get; init; }

        public string? Cover { get; init; }

        /// <summary>Format, bit depth and sample rate, as far as they are known. Null when none of them is.</summary>
        public string? Quality { get; init; }

        /// <summary>Length in seconds, or null for a stream that has no end.</summary>
        public double? Duration { get; init; }

        /// <summary>True when the scene may cut rather than merely being corrected.</summary>
        public bool NewTrack { get; init; }

        /// <summary>Builds the message, or null when there is nothing worth telling the television.</summary>
        public static StageUpdate? For(NowPlayingTrack track, string? zone, string? coverUrl, bool isNewTrack)
        {
            if (track == null || track.IsEmpty)
                return null;

            return new StageUpdate
            {
                Title = Blank(track.Title),
                Artist = Blank(track.Artist),
                Album = Blank(track.Album),
                Zone = Blank(zone),
                Cover = Blank(coverUrl),
                Quality = QualityMark(track),
                Duration = track.Duration > TimeSpan.Zero ? track.Duration.Value.TotalSeconds : null,
                NewTrack = isNewTrack
            };
        }

        /// <summary>
        /// The small line in the corner: "FLAC · 24 Bit · 96 kHz". Only the parts the file actually said.
        /// </summary>
        private static string? QualityMark(NowPlayingTrack track)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(track.Format))
                parts.Add(track.Format.Trim().ToUpperInvariant());

            if (track.BitDepth > 0)
                parts.Add(track.BitDepth.Value.ToString(CultureInfo.InvariantCulture) + " Bit");

            if (track.SampleRate > 0)
                parts.Add(Kilohertz(track.SampleRate.Value));

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>
        /// "44,1 kHz" rather than "44100 Hz": the first is what is printed on a sleeve, the second is a
        /// number out of a datasheet. The comma is deliberate - this line is read, not parsed.
        /// </summary>
        private static string Kilohertz(int hertz)
        {
            var khz = hertz / 1000.0;
            var rounded = Math.Round(khz, 1);
            return rounded == Math.Floor(rounded)
                ? ((int)rounded).ToString(CultureInfo.InvariantCulture) + " kHz"
                : rounded.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")) + " kHz";
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
