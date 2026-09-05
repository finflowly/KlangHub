using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KlangHub.Core.Diagnostics
{
    /// <summary>
    /// The block at the top of every log: what was running, on what, configured how.
    ///
    /// Without it a log from a stranger is close to useless - the first three questions about any report
    /// are which version, which Windows and which audio format, and none of them could be answered from
    /// the message stream alone. It is written once at startup and again whenever a setting changes that
    /// would alter the interpretation of everything below it.
    /// </summary>
    public static class DiagnosticsHeader
    {
        /// <param name="values">Ordered field name to value. A field with no value is left out rather than
        /// printed empty - an unanswered question is worse than an absent one.</param>
        public static string Build(IEnumerable<KeyValuePair<string, string?>> values, DateTime now)
        {
            var rows = new List<KeyValuePair<string, string>>();
            foreach (var pair in values)
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    rows.Add(new KeyValuePair<string, string>(pair.Key, pair.Value!.Trim()));

            int width = 0;
            foreach (var row in rows)
                width = Math.Max(width, row.Key.Length);

            var text = new StringBuilder();
            text.Append("=== KlangHub diagnostics ")
                .Append(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .AppendLine(" ===");

            foreach (var row in rows)
                text.Append("  ").Append(row.Key.PadRight(width)).Append("  ").AppendLine(row.Value);

            text.AppendLine(new string('=', 48));
            return text.ToString();
        }
    }
}
