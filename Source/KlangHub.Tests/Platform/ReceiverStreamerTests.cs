using System;
using System.IO;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ReceiverStreamerTests
    {
        private static string Receiver() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "receiver", "index.html"));

        [Fact]
        public void A_stage_message_that_arrives_as_text_is_read_as_the_message_it_is()
        {
            var html = Receiver();

            Assert.Contains("typeof d === 'string'", html);
            Assert.Contains("JSON.parse(d)", html);
        }

        [Fact]
        public void Text_that_is_not_a_message_is_dropped_instead_of_thrown()
        {
            var html = Receiver();
            var listener = Between(html, "function readStage(", "function wireStage(");

            Assert.Contains("catch", listener);
            Assert.Contains("return null;", listener);
        }

        [Fact]
        public void The_same_listeners_serve_the_television_and_the_lab()
        {
            var html = Receiver();

            Assert.Contains("function wireStage(", html);
            Assert.Contains("wireStage(cast.framework,", html);
            Assert.Contains("wireStage(fake.framework,", html);
        }

        [Fact]
        public void A_load_whose_event_carries_no_media_still_finds_the_words()
        {
            var html = Receiver();

            Assert.Contains("mediaWorthShowing(event.media)", html);
            Assert.Contains("getMediaInformation()", html);
        }

        [Fact]
        public void The_lab_can_pretend_to_be_the_streamer_that_reported_the_fault()
        {
            var html = Receiver();

            Assert.Contains("device=", html);
            Assert.Contains("streamer", html);
        }

        [Fact]
        public void The_lab_says_what_it_is_doing_so_a_run_can_be_read_afterwards()
        {
            Assert.Contains("console.log('lab: ", Receiver());
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
