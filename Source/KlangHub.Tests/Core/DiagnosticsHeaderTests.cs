using System;
using System.Collections.Generic;
using KlangHub.Core.Diagnostics;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class DiagnosticsHeaderTests
    {
        private static readonly DateTime T = new(2026, 9, 5, 8, 24, 4, DateTimeKind.Local);

        private static KeyValuePair<string, string?> P(string key, string? value) => new(key, value);

        [Fact]
        public void It_names_the_moment_it_was_written()
        {
            var header = DiagnosticsHeader.Build(new[] { P("Version", "1.0") }, T);
            Assert.Contains("2026-09-05 08:24:04", header);
        }

        [Fact]
        public void Fields_are_aligned_so_the_block_can_be_read_at_a_glance()
        {
            var header = DiagnosticsHeader.Build(new[] { P("Version", "1.0"), P("Audio format", "FLAC 24 bit") }, T);
            Assert.Contains("  Version       1.0", header);
            Assert.Contains("  Audio format  FLAC 24 bit", header);
        }

        [Fact]
        public void A_field_we_could_not_determine_is_left_out_rather_than_shown_empty()
        {
            var header = DiagnosticsHeader.Build(
                new[] { P("Version", "1.0"), P("Receiver app id", null), P("Windows", "   ") }, T);

            Assert.DoesNotContain("Receiver app id", header);
            Assert.DoesNotContain("Windows", header);
            Assert.Contains("1.0", header);
        }

        [Fact]
        public void The_order_given_is_the_order_shown()
        {
            var header = DiagnosticsHeader.Build(
                new[] { P("First", "a"), P("Second", "b"), P("Third", "c") }, T);

            Assert.True(header.IndexOf("First", StringComparison.Ordinal) < header.IndexOf("Second", StringComparison.Ordinal));
            Assert.True(header.IndexOf("Second", StringComparison.Ordinal) < header.IndexOf("Third", StringComparison.Ordinal));
        }

        [Fact]
        public void An_empty_set_still_produces_a_readable_block()
        {
            var header = DiagnosticsHeader.Build(Array.Empty<KeyValuePair<string, string?>>(), T);
            Assert.Contains("KlangHub diagnostics", header);
        }
    }
}
