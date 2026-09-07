using System;
using System.IO;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ReceiverCoverReachTests
    {
        private static string Receiver() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "receiver", "index.html"));

        private static string SetCover() => Between(Receiver(), "function setCover(", "function albumWorthPrinting(");

        [Fact]
        public void The_picture_that_is_shown_is_never_asked_for_permission_to_be_read()
        {
            var shown = Between(SetCover(), "var picture = new Image();", "var tint = new Image();");

            Assert.DoesNotContain("crossOrigin", shown);
        }

        [Fact]
        public void The_colour_is_taken_from_a_second_picture_that_is_allowed_to_fail()
        {
            var setCover = SetCover();

            Assert.Contains("var tint = new Image();", setCover);
            Assert.Contains("tint.crossOrigin = 'anonymous';", setCover);
        }

        [Fact]
        public void A_colour_that_cannot_be_read_leaves_the_picture_where_it_is()
        {
            var tint = Between(SetCover(), "var tint = new Image();", "tint.src = url;");

            Assert.DoesNotContain("classList.add('empty')", tint);
            Assert.DoesNotContain("shown.cover", tint);
        }

        [Fact]
        public void Only_the_picture_itself_failing_takes_the_words_back_to_the_rings()
        {
            var picture = Between(SetCover(), "var picture = new Image();", "var tint = new Image();");

            Assert.Contains("picture.onerror", picture);
            Assert.Contains("el.art.classList.add('empty')", picture);
        }

        private static string Between(string text, string from, string to)
        {
            var start = text.IndexOf(from, StringComparison.Ordinal);
            Assert.True(start >= 0, $"'{from}' is not in the receiver");

            var end = text.IndexOf(to, start, StringComparison.Ordinal);
            Assert.True(end > start, $"'{to}' does not follow '{from}' in the receiver");

            return text[start..end];
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
