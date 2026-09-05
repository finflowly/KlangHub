using KlangHub.Communication;
using KlangHub.Core.NowPlaying;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// The messages KlangHub sends to its own receiver over its own namespace. This is the channel that
    /// lets the television change what it shows while the audio keeps running - the alternative, a fresh
    /// LOAD per track, means one to three seconds of silence every time a song ends.
    /// </summary>
    public class StageMessageTests
    {
        private const string Namespace = "urn:x-cast:de.klanghub.stage";

        [Fact]
        public void A_track_message_travels_on_our_own_namespace()
        {
            // The namespace must match the one the receiver registers, character for character; a mismatch
            // is silent on both ends - the device simply never delivers the message.
            var msg = new ChromeCastMessages().GetStageTrackMessage(
                StageUpdate.For(new NowPlayingTrack { Title = "Teardrop" }, null, null, true)!, "client-1", "web-1");

            Assert.Equal(Namespace, msg.Namespace);
        }

        [Fact]
        public void A_track_message_carries_the_piece()
        {
            var update = StageUpdate.For(
                new NowPlayingTrack { Title = "Teardrop", Artist = "Massive Attack", Album = "Mezzanine" },
                "Wohnzimmer", "http://host/artwork.png", true)!;

            var payload = new ChromeCastMessages().GetStageTrackMessage(update, "client-1", "web-1").PayloadUtf8;

            Assert.Contains("\"type\":\"track\"", payload);
            Assert.Contains("\"title\":\"Teardrop\"", payload);
            Assert.Contains("\"artist\":\"Massive Attack\"", payload);
            Assert.Contains("\"album\":\"Mezzanine\"", payload);
            Assert.Contains("\"zone\":\"Wohnzimmer\"", payload);
            Assert.Contains("\"newTrack\":true", payload);
        }

        [Fact]
        public void Fields_we_do_not_know_are_left_out_entirely()
        {
            // Not sent at all, rather than sent as null or "". The receiver merges what it is given and
            // keeps the rest; a key that arrives empty would wipe a line it is showing correctly.
            var update = StageUpdate.For(new NowPlayingTrack { Title = "Teardrop" }, null, null, false)!;

            var payload = new ChromeCastMessages().GetStageTrackMessage(update, "client-1", "web-1").PayloadUtf8;

            Assert.DoesNotContain("artist", payload);
            Assert.DoesNotContain("album", payload);
            Assert.DoesNotContain("null", payload);
        }

        [Fact]
        public void A_position_message_is_small_because_it_is_sent_often()
        {
            var payload = new ChromeCastMessages().GetStagePositionMessage(134.5, 330, "client-1", "web-1").PayloadUtf8;

            Assert.Contains("\"type\":\"position\"", payload);
            Assert.Contains("134.5", payload);
            Assert.Contains("330", payload);
            Assert.DoesNotContain("title", payload);
        }

        [Fact]
        public void A_state_message_says_only_whether_it_is_playing()
        {
            var payload = new ChromeCastMessages().GetStageStateMessage(false, "client-1", "web-1").PayloadUtf8;

            Assert.Contains("\"type\":\"state\"", payload);
            Assert.Contains("\"playing\":false", payload);
        }

        [Fact]
        public void Stage_messages_go_to_the_receiver_application_not_the_platform()
        {
            // sender-0/receiver-0 is the device's own platform channel; our receiver lives at the transport
            // id the RECEIVER_STATUS handed back, and a message sent to the platform never reaches it.
            var msg = new ChromeCastMessages().GetStageStateMessage(true, "client-8123", "web-42");

            Assert.Equal("client-8123", msg.SourceId);
            Assert.Equal("web-42", msg.DestinationId);
        }
    }
}
