using System;
using System.IO;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ReceiverCoverResetTests
    {
        private static string Receiver() =>
            File.ReadAllText(Path.Combine(RepositoryRoot(), "receiver", "index.html"));

        [Fact]
        public void A_new_track_without_a_cover_clears_the_one_before_it()
        {
            Assert.Contains("if (isNewTrack) setCover(update.cover || '');", Receiver());
        }

        [Fact]
        public void A_merge_without_a_cover_keeps_the_cover_of_this_track()
        {
            Assert.Contains("else if (update.cover !== undefined) setCover(update.cover);", Receiver());
        }

        [Fact]
        public void A_load_without_images_does_not_inherit_the_pictures_of_the_load_before_it()
        {
            var html = Receiver();

            Assert.Contains("if (media) loadedMedia = media;", html);
            Assert.DoesNotContain("if (media && media.metadata) loadedMedia = media;", html);
        }

        [Fact]
        public void A_new_track_without_a_length_takes_the_progress_bar_away()
        {
            Assert.Contains("duration = typeof update.duration === 'number' ? update.duration : 0;", Receiver());
        }

        [Fact]
        public void A_stage_message_arrives_when_the_receiver_never_learned_a_token()
        {
            Assert.Contains("if (!sessionToken) return true;", Receiver());
        }

        [Fact]
        public void The_radio_demo_shows_the_rings_it_would_show_on_a_television()
        {
            var html = Receiver();
            var radio = html[html.IndexOf("if (demo === 'radio')", StringComparison.Ordinal)..];
            var block = radio[..radio.IndexOf("if (demo === 'paused')", StringComparison.Ordinal)];

            Assert.DoesNotContain("demoCover()", block);
        }


        [Fact]
        public void Words_that_change_with_no_cover_on_either_side_do_not_pay_for_a_cut()
        {
            Assert.Contains("if (!shown.cover && !update.cover) { render(update, true); return; }", Receiver());
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
