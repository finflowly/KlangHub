using KlangHub.Communication;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Which application in a RECEIVER_STATUS is ours. The launch already honours a pasted id, but the
    /// answer was matched against a hard-coded "CC1AD845" - so the moment somebody entered KlangHub's own
    /// receiver id the launch succeeded and the reply was thrown away: no transport id, no session, no
    /// media load, and a card that sits at "connecting" forever.
    /// </summary>
    public class CastReceiverAppIdTests
    {
        /// <summary>
        /// A made-up id. A real one belongs to whoever registered it in the Cast Developer Console and to
        /// their Google account - which is exactly why KlangHub keeps it in the settings rather than in the
        /// code, and why one does not belong in a public test either.
        /// </summary>
        private const string ExampleAppId = "A1B2C3D4";

        [Fact]
        public void No_id_configured_means_googles_default_receiver()
        {
            Assert.True(CastReceiver.Matches(string.Empty, ChromeCastMessages.DefaultReceiverAppId));
        }

        [Fact]
        public void A_configured_id_is_the_one_we_look_for()
        {
            Assert.True(CastReceiver.Matches("A1B2C3D4", "A1B2C3D4"));
        }

        [Fact]
        public void A_configured_id_no_longer_answers_to_the_default_receiver()
        {
            // The whole bug in one line: our own receiver is running, Google's is not.
            Assert.False(CastReceiver.Matches("A1B2C3D4", ChromeCastMessages.DefaultReceiverAppId));
        }

        [Theory]
        [InlineData("a1b2c3d4", "A1B2C3D4")]
        [InlineData("  A1B2C3D4  ", "A1B2C3D4")]
        public void Forgives_how_the_id_was_pasted(string configured, string reported)
        {
            // Cast ids are written in capitals, but a person copying one out of the developer console
            // should not lose their evening to a stray space or a lowercase letter.
            Assert.True(CastReceiver.Matches(configured, reported));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void An_application_without_an_id_is_never_ours(string? reported)
        {
            Assert.False(CastReceiver.Matches("A1B2C3D4", reported));
        }
    }
}
