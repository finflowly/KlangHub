using System;
using System.IO;
using System.Linq;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ReceiverCutTests
    {
        private static string Receiver() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "receiver", "index.html"));

        [Fact]
        public void A_track_change_is_not_revealed_by_a_timer_alone()
        {
            var html = Receiver();

            Assert.Contains("requestAnimationFrame(awaitCut)", html);
            Assert.Contains("setTimeout(settle, CUT_MS)", html);
        }

        [Fact]
        public void Whichever_clock_wins_the_cut_lands_exactly_once()
        {
            var html = Receiver();

            Assert.Contains("function settle()", html);
            Assert.Contains("if (!pending) return;", html);
        }

        [Fact]
        public void An_arriving_message_lands_a_cut_that_is_already_overdue()
        {
            var html = Receiver();

            Assert.Contains("function settleIfOverdue()", html);
            Assert.Contains("settleIfOverdue();", html);
        }

        [Fact]
        public void The_words_on_a_load_never_depend_on_one_source_for_the_media()
        {
            var html = Receiver();

            Assert.Contains("function mediaWorthShowing(", html);
            Assert.Contains("getMediaInformation()", html);
            Assert.Contains("loadedMedia", html);
        }

        [Theory]
        [InlineData("stage-report")]
        [InlineData("holdTheBottomRow")]
        [InlineData("bottomRowNudge")]
        [InlineData("sayToSender")]
        [InlineData("gapLongest")]
        public void The_measuring_machinery_is_gone_from_the_receiver(string removed)
        {
            Assert.DoesNotContain(removed, Receiver());
        }

        [Theory]
        [InlineData("color-mix(")]
        [InlineData("clamp(")]
        public void A_value_a_older_television_cannot_read_is_preceded_by_one_it_can(string feature)
        {
            var lines = Lines();

            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains(feature))
                    continue;

                var property = Property(lines[i]);
                if (property == null)
                    continue;

                var backed = false;
                for (var back = i - 1; back >= 0 && back >= i - 5 && !backed; back--)
                    backed = Property(lines[back]) == property && !lines[back].Contains(feature);

                Assert.True(backed,
                    $"{feature} on line {i + 1} has no plain fallback before it: {lines[i].Trim()}");
            }
        }

        [Fact]
        public void Every_inset_is_preceded_by_the_four_sides_it_stands_for()
        {
            var lines = Lines();

            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("inset:"))
                    continue;

                var before = string.Join(" ", lines.Skip(Math.Max(0, i - 2)).Take(i - Math.Max(0, i - 2)))
                             + " " + lines[i][..lines[i].IndexOf("inset:", StringComparison.Ordinal)];

                Assert.True(before.Contains("top:") && before.Contains("bottom:")
                            && before.Contains("left:") && before.Contains("right:"),
                    $"inset on line {i + 1} has no four-sided fallback before it: {lines[i].Trim()}");
            }
        }

        private static string[] Lines() => Receiver().Replace("\r\n", "\n").Split('\n');

        private static string? Property(string line)
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                return null;

            var name = line[..colon].Trim();
            return name.Length > 0 && name.All(c => char.IsLetter(c) || c == '-') ? name : null;
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
                directory = directory.Parent;

            Assert.True(directory != null, "Could not find the repository root from " + AppContext.BaseDirectory);
            return directory!.FullName;
        }
    }
}
