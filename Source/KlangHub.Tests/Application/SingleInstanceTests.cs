using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Application
{
    /// <summary>
    /// Only one KlangHub per signed-in user.
    /// <para>
    /// The check the project inherited was commented out, which mattered little while closing the window
    /// ended the program. Now the close button hides it in the notification area - so clicking the desktop
    /// icon again is the obvious thing to do, and it started a second copy that fought the first one for the
    /// REST port, the streaming port and the audio device. It shows up in a log as one line and then a
    /// silence: "normally only one use of each socket address is permitted".
    /// </para>
    /// </summary>
    public class SingleInstanceTests
    {
        [Fact]
        public void The_first_copy_gets_it_and_the_second_does_not()
        {
            using var first = SingleInstance.TryAcquire();
            Assert.True(first.IsOnlyInstance);

            using var second = SingleInstance.TryAcquire();
            Assert.False(second.IsOnlyInstance);
        }

        [Fact]
        public void The_next_copy_may_start_once_the_first_has_gone()
        {
            using (var first = SingleInstance.TryAcquire())
                Assert.True(first.IsOnlyInstance);

            using var next = SingleInstance.TryAcquire();
            Assert.True(next.IsOnlyInstance);
        }

        [Fact]
        public void Two_people_signed_in_at_once_each_get_their_own()
        {
            // "Local\" scopes the name to the logon session. Without it, the first person to sign in would
            // silently prevent the second from running KlangHub at all - on a family PC with fast user
            // switching, that is a bug nobody would ever diagnose.
            Assert.StartsWith(@"Local\", SingleInstance.Name);
        }

        [Fact]
        public void The_name_says_nothing_about_who_is_running_it()
        {
            // A kernel object name is visible to every process in the session. It carries a fixed
            // identifier and nothing else - no user name, no path, no machine.
            Assert.Equal(@"Local\KlangHub-c0ffee42-9b1e-4f7a-8a53-3d2c6b5e10a4", SingleInstance.Name);
        }
    }
}
