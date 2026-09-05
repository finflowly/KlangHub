using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KlangHub.Core.Diagnostics
{
    /// <summary>
    /// Turns one raw log message into a line somebody else can actually read.
    ///
    /// Written for the case that matters: a user we have never met sends us their log because casting
    /// misbehaved. Three things decide whether that log is worth anything -
    ///   every line carries the time, so events can be correlated (a stream dying, and an mDNS burst
    ///     sixteen seconds earlier, only become a story when both are stamped);
    ///   failures look like failures, not like ordinary chatter;
    ///   the recurring status reports do not bury everything else.
    /// The third is why the status messages are summarised: one MEDIA_STATUS is around 1500 characters, of
    /// which the title, the artwork URL and the media block repeat unchanged every fifteen seconds, per
    /// device. What actually varies is the player state, the position and the volume.
    /// </summary>
    public static class LogLine
    {
        /// <summary>A timestamp several call sites already embed, e.g. "out [08:33:19][192.168.8.198:8009]".
        /// It is dropped in favour of the one this class puts in front, which has milliseconds.</summary>
        private static readonly Regex EmbeddedTime = new(
            @"^(?<lead>(in|out|ex)?\s*)\[\d{1,2}[:.]\d{2}[:.]\d{2}\]\s*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PlayerState = new("\"playerState\":\"(?<v>[A-Z_]+)\"", RegexOptions.Compiled);
        private static readonly Regex CurrentTime = new("\"currentTime\":(?<v>-?[0-9.]+)", RegexOptions.Compiled);
        private static readonly Regex VolumeLevel = new("\"level\":(?<v>-?[0-9.eE+-]+)", RegexOptions.Compiled);
        private static readonly Regex VolumeMuted = new("\"muted\":(?<v>true|false)", RegexOptions.Compiled);
        private static readonly Regex Request = new("\"requestId\":(?<v>\\d+)", RegexOptions.Compiled);

        public enum Severity { Info, Warning, Error }

        public static string Format(string? message, DateTime now)
        {
            var text = (message ?? string.Empty).Trim();
            text = EmbeddedTime.Replace(text, "${lead}");

            var marker = SeverityOf(text) switch
            {
                Severity.Error => " ERR ",
                Severity.Warning => " WRN ",
                _ => "     ",
            };

            return now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + marker + Summarise(text);
        }

        /// <summary>What kind of line this is. Only what the text itself proves - a message that merely
        /// mentions an error state is not itself a failure.</summary>
        public static Severity SeverityOf(string? message)
        {
            var text = message ?? string.Empty;
            if (text.StartsWith("ex ", StringComparison.Ordinal) || text.StartsWith("ex:", StringComparison.Ordinal))
                return Severity.Error;

            if (text.Contains("[ConnectError]", StringComparison.Ordinal)
                || text.Contains("Disconnected", StringComparison.Ordinal)
                || text.Contains("Connection closed", StringComparison.Ordinal)
                || text.Contains("LoadFailed", StringComparison.Ordinal))
                return Severity.Warning;

            return Severity.Info;
        }

        /// <summary>
        /// Collapses the two messages that repeat forever. Everything else - LOAD, LAUNCH, CONNECT, CLOSE,
        /// a status that names a running application, and every message we write ourselves - is left exactly
        /// as it was, because those are the ones worth reading in full.
        /// </summary>
        public static string Summarise(string message)
        {
            if (message.Contains("\"applications\"", StringComparison.Ordinal))
                return message;      // a receiver status naming the running app is worth its length

            if (message.Contains("\"type\":\"MEDIA_STATUS\"", StringComparison.Ordinal))
            {
                var head = HeadOf(message);
                var state = PlayerState.Match(message);
                if (!state.Success)
                    return head + "MEDIA_STATUS (empty)" + RequestOf(message);

                var position = CurrentTime.Match(message);
                var seconds = position.Success
                    && double.TryParse(position.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
                        ? " t=" + t.ToString("F1", CultureInfo.InvariantCulture) + "s"
                        : string.Empty;

                return head + "MEDIA_STATUS " + state.Groups["v"].Value + seconds + VolumeOf(message) + RequestOf(message);
            }

            if (message.Contains("\"type\":\"RECEIVER_STATUS\"", StringComparison.Ordinal))
                return HeadOf(message) + "RECEIVER_STATUS" + VolumeOf(message) + RequestOf(message);

            return message;
        }

        /// <summary>Everything before the JSON - the direction, the endpoint and the state the sender was in.
        /// That prefix is what a reader navigates by, so it always survives.</summary>
        private static string HeadOf(string message)
        {
            int brace = message.IndexOf('{');
            return brace <= 0 ? string.Empty : message[..brace].TrimEnd() + " ";
        }

        private static string VolumeOf(string message)
        {
            var level = VolumeLevel.Match(message);
            if (!level.Success)
                return string.Empty;

            var text = double.TryParse(level.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? " vol=" + v.ToString("F2", CultureInfo.InvariantCulture)
                : " vol=" + level.Groups["v"].Value;

            var muted = VolumeMuted.Match(message);
            return muted.Success && muted.Groups["v"].Value == "true" ? text + " muted" : text;
        }

        private static string RequestOf(string message)
        {
            var id = Request.Match(message);
            return id.Success ? " #" + id.Groups["v"].Value : string.Empty;
        }
    }
}
