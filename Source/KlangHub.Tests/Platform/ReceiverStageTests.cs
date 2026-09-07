using System;
using System.IO;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ReceiverStageTests
    {
        private static string Receiver() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "receiver", "index.html"));

        [Fact]
        public void The_receiver_learns_the_session_token_from_the_load()
        {
            var html = Receiver();

            Assert.Contains("setMessageInterceptor", html);
            Assert.Contains("stageToken", html);
        }

        [Fact]
        public void A_stage_message_is_ignored_unless_it_carries_that_token()
        {
            var html = Receiver();

            Assert.Contains("function fromUs(", html);
            Assert.Contains("if (!fromUs(", html);
        }

        [Fact]
        public void No_text_from_the_network_is_ever_written_as_markup()
        {
            Assert.DoesNotContain("innerHTML", Receiver());
        }

        [Fact]
        public void A_cover_address_is_http_or_https_and_nothing_else()
        {
            Assert.Contains("/^https?:\\/\\//i.test(url)", Receiver());
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
