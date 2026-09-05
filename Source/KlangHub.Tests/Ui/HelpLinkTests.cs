using System;
using System.IO;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// "Help and documentation" is the second click a new user makes, right after the README. It used to
    /// open the fork's wiki (<c>.../wiki#options</c>) - a page this project never wrote, so the link led to
    /// an empty wiki with an anchor that matched nothing.
    /// <para>
    /// A link into the repository can be checked; a link into a wiki cannot, because a wiki is a separate
    /// repository that is not cloned with this one. So the help link points at a file that is committed
    /// beside the code, and this test holds the two together: change the URL, or move the document, and the
    /// build says so instead of a user finding out.
    /// </para>
    /// </summary>
    public class HelpLinkTests
    {
        [Fact]
        public void The_help_link_is_an_https_github_url()
        {
            Assert.True(Uri.TryCreate(KlangHub.MainForm.HelpUrl, UriKind.Absolute, out var url),
                $"the help link is not an absolute URL: '{KlangHub.MainForm.HelpUrl}'");
            Assert.Equal(Uri.UriSchemeHttps, url!.Scheme);
            Assert.Equal("github.com", url.Host);
        }

        [Fact]
        public void The_help_link_points_at_a_document_that_exists_in_this_repository()
        {
            var full = Path.Combine(RepositoryRoot(),
                LinkedFile().Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(full),
                $"the help link points at '{LinkedFile()}', which is not in the repository.");
        }

        [Fact]
        public void The_help_document_says_something_about_the_settings_the_link_came_from()
        {
            // The old link carried an "#options" anchor: the user clicked help FROM the settings tab and
            // expected to land on the settings. The anchor is gone, so the document itself has to earn it.
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(),
                LinkedFile().Replace('/', Path.DirectorySeparatorChar)));

            Assert.Contains("Settings", text, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The repository-relative path the help link names, from
        /// <c>https://github.com/&lt;owner&gt;/&lt;repo&gt;/blob/&lt;ref&gt;/&lt;path...&gt;</c>.
        /// Fails with the reason rather than an index exception when the URL is not that shape at all -
        /// which is precisely the case the dead wiki link was.
        /// </summary>
        private static string LinkedFile()
        {
            var url = new Uri(KlangHub.MainForm.HelpUrl);
            var segments = url.AbsolutePath.Trim('/').Split('/');

            Assert.True(segments.Length > 4 && segments[2] == "blob",
                $"the help link is not a link to a file in the repository: '{url}'. A wiki page cannot be "
                + "verified from here - that is how the dead one survived.");

            return string.Join('/', segments[4..]);
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
