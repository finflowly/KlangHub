using KlangHub.Platform.Clementine;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Which of Clementine's URLs become a path this machine will open.
    /// <para>
    /// The answer arrives over a loopback socket with no authentication at all - Clementine's remote
    /// protocol has an auth code and KlangHub sends 0 - so it is whatever is listening on that port, not
    /// necessarily Clementine. The path it names is then handed to the tag reader and the cover hunt, and
    /// whatever they find is served to every device on the network.
    /// </para>
    /// </summary>
    public class ClementineLocalPathTests
    {
        [Fact]
        public void A_local_file_becomes_a_local_path()
            => Assert.Equal(@"D:\Musik\Aquanote\03 Nowhere.flac",
                            ClementineSong.LocalPath("file:///D:/Musik/Aquanote/03%20Nowhere.flac"));

        [Fact]
        public void A_path_on_another_machine_is_refused()
        {
            // Uri.IsFile is true for a UNC path as well, so this used to come back as a path and the tag
            // reader opened an SMB connection to a host of the sender's choosing - with the signed-in
            // user's credentials offered to it as part of the handshake.
            Assert.Null(ClementineSong.LocalPath("file://somewhere-else/share/x.flac"));
            Assert.Null(ClementineSong.LocalPath(@"\\somewhere-else\share\x.flac"));
        }

        [Theory]
        [InlineData("http://stream.example.invalid/live")]
        [InlineData("https://stream.example.invalid/live")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not a url at all")]
        public void Anything_that_is_not_a_local_file_yields_nothing(string? url)
            => Assert.Null(ClementineSong.LocalPath(url));
    }
}
