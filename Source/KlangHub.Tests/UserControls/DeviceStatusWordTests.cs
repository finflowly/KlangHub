using KlangHub.Core.Casting;
using KlangHub.UserControls;
using Xunit;

namespace KlangHub.Tests.UserControls
{
    /// <summary>
    /// The word under a device name is the only thing most people ever read about a connection, so it has
    /// to be true.
    /// <para>
    /// It was not. Every state the mapping did not name fell into a catch-all that said "connected" -
    /// including <see cref="PlaybackState.Unknown"/>, which is precisely the state of a device that has
    /// stopped answering. A television behind a VPN with no route to the local network showed a red dot and
    /// the word "connected" at the same time, alternating every fifteen seconds. Half of that flicker was
    /// the network; this half was the card contradicting itself.
    /// </para>
    /// </summary>
    public class DeviceStatusWordTests
    {
        private static string Connected => KlangHub.Properties.Strings.Card_Status_Connected_Text;

        [Fact]
        public void A_device_in_an_unknown_state_does_not_claim_to_be_connected()
        {
            Assert.NotEqual(Connected, DeviceControl.StatusWord(PlaybackState.Unknown));
        }

        [Fact]
        public void A_disconnected_device_does_not_claim_to_be_connected()
        {
            Assert.NotEqual(Connected, DeviceControl.StatusWord(PlaybackState.Disconnected));
        }

        [Fact]
        public void Unknown_and_disconnected_read_the_same()
        {
            // A listener cannot act on the difference between "we lost it" and "we never knew": both mean
            // the device is not there. One word for both keeps the card from flickering between two
            // spellings of the same fact.
            Assert.Equal(DeviceControl.StatusWord(PlaybackState.Disconnected),
                         DeviceControl.StatusWord(PlaybackState.Unknown));
        }

        [Fact]
        public void A_device_that_is_connected_and_idle_says_connected()
        {
            Assert.Equal(Connected, DeviceControl.StatusWord(PlaybackState.Connected));
            Assert.Equal(Connected, DeviceControl.StatusWord(PlaybackState.Idle));
        }

        [Fact]
        public void Every_state_has_a_word_of_its_own_meaning()
        {
            // Guards the catch-all from growing back: if a new state is added without a word, it must not
            // silently inherit "connected".
            foreach (PlaybackState state in System.Enum.GetValues<PlaybackState>())
            {
                var word = DeviceControl.StatusWord(state);
                Assert.False(string.IsNullOrWhiteSpace(word), $"{state} has no word");
                if (state != PlaybackState.Connected && state != PlaybackState.Idle)
                    Assert.NotEqual(Connected, word);
            }
        }
    }
}
