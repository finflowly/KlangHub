using KlangHub.Core.Diagnostics;
using Xunit;

namespace KlangHub.Tests.Core
{
    public class ReleaseVersionTests
    {
        [Theory]
        [InlineData("v1.0.0", "1.0")]      // the conventional tag for the build we are running
        [InlineData("1.0", "1.0")]
        [InlineData("v1.0", "1.0.0")]
        [InlineData("1.0.0.0", "1.0")]
        public void The_release_we_are_already_running_is_not_offered_again(string tag, string current)
        {
            Assert.False(ReleaseVersion.IsNewer(tag, current), tag + " vs " + current);
        }

        [Theory]
        [InlineData("v1.1", "1.0")]
        [InlineData("v1.0.1", "1.0")]
        [InlineData("v2.0", "1.9")]
        [InlineData("v1.10", "1.9")]       // text comparison got this one backwards
        public void A_later_release_is_recognised(string tag, string current)
        {
            Assert.True(ReleaseVersion.IsNewer(tag, current), tag + " vs " + current);
        }

        [Theory]
        [InlineData("v0.9", "1.0")]
        [InlineData("v1.0", "1.1")]
        [InlineData("v1.9", "1.10")]
        public void An_older_release_is_not_offered(string tag, string current)
        {
            Assert.False(ReleaseVersion.IsNewer(tag, current), tag + " vs " + current);
        }

        [Theory]
        [InlineData(null, "1.0")]
        [InlineData("", "1.0")]
        [InlineData("nightly", "1.0")]
        [InlineData("v1.0", null)]
        public void Something_we_cannot_read_is_never_announced_as_an_update(string? tag, string? current)
        {
            Assert.False(ReleaseVersion.IsNewer(tag, current));
        }

        [Theory]
        [InlineData("v1.2.3", 1, 2, 3)]
        [InlineData("release-2.0", 2, 0, -1)]
        [InlineData("1.0", 1, 0, -1)]
        public void A_decorated_tag_still_yields_its_version(string tag, int major, int minor, int build)
        {
            var version = ReleaseVersion.Parse(tag);
            Assert.NotNull(version);
            Assert.Equal(major, version!.Major);
            Assert.Equal(minor, version.Minor);
            Assert.Equal(build, version.Build);
        }
    }
}
