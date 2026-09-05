using System;
using KlangHub.Core.Diagnostics;
using Xunit;

namespace KlangHub.Tests.Core
{
    /// <summary>The samples are real lines from a measured device log of 2026-09-05.</summary>
    public class LogLineTests
    {
        private static readonly DateTime T = new(2026, 9, 5, 8, 33, 19, 123, DateTimeKind.Local);

        private const string MediaStatus =
            "in [08:33:19] [192.168.8.198:8009] [Playing]: {\"type\":\"MEDIA_STATUS\",\"status\":[{\"mediaSessionId\":1," +
            "\"playbackRate\":1,\"playerState\":\"PLAYING\",\"currentTime\":529.232358,\"supportedMediaCommands\":274447," +
            "\"volume\":{\"level\":1,\"muted\":false},\"media\":{\"contentId\":\"http://192.168.8.167:52363/\"," +
            "\"contentType\":\"audio/flac\",\"metadata\":{\"title\":\"KlangHub\",\"images\":[{\"url\":\"http://x/artwork.png\"}]}}}]," +
            "\"requestId\":41}";

        [Fact]
        public void Every_line_starts_with_the_time_to_the_millisecond()
        {
            Assert.StartsWith("08:33:19.123", LogLine.Format("Recording Stopped", T));
        }

        [Fact]
        public void An_already_embedded_timestamp_is_not_repeated()
        {
            var line = LogLine.Format(MediaStatus, T);
            Assert.StartsWith("08:33:19.123", line);
            Assert.DoesNotContain("[08:33:19]", line);
            Assert.Contains("in [192.168.8.198:8009] [Playing]:", line);
        }

        [Fact]
        public void A_media_status_keeps_what_varies_and_drops_what_repeats()
        {
            var line = LogLine.Format(MediaStatus, T);
            Assert.Contains("MEDIA_STATUS PLAYING t=529.2s vol=1.00 #41", line);
            Assert.DoesNotContain("artwork", line);
            Assert.DoesNotContain("contentType", line);
            Assert.True(line.Length < 110, "still " + line.Length + " characters: " + line);
        }

        [Fact]
        public void A_muted_device_says_so()
        {
            var status = "in [192.168.8.182:8009] [Connected]: {\"requestId\":39,\"status\":{\"volume\":" +
                         "{\"controlType\":\"master\",\"level\":0.0099,\"muted\":true}},\"type\":\"RECEIVER_STATUS\"}";
            Assert.Contains("RECEIVER_STATUS vol=0.01 muted #39", LogLine.Format(status, T));
        }

        [Fact]
        public void A_status_that_names_the_running_application_is_kept_in_full()
        {
            var launch = "in [192.168.8.198:8009] [LaunchingApplication]: {\"requestId\":4,\"status\":{\"applications\":" +
                         "[{\"appId\":\"CC1AD845\",\"sessionId\":\"6ad41d57\"}]},\"type\":\"RECEIVER_STATUS\"}";
            Assert.Contains("\"sessionId\":\"6ad41d57\"", LogLine.Format(launch, T));
        }

        [Fact]
        public void A_load_is_never_shortened()
        {
            var load = "out [192.168.8.198:8009] [LoadingMedia]: {\"media\":{\"contentId\":\"http://x/\"," +
                       "\"contentType\":\"audio/flac\"},\"requestId\":5,\"type\":\"LOAD\"}";
            Assert.Contains("audio/flac", LogLine.Format(load, T));
        }

        [Fact]
        public void Failures_are_marked_so_they_can_be_found()
        {
            Assert.Contains(" ERR ", LogLine.Format("ex [192.168.8.204]: Connect The operation has timed out.", T));
            Assert.Contains(" WRN ", LogLine.Format("out [192.168.8.198:8009] [ConnectError]: {\"type\":\"GET_STATUS\"}", T));
            Assert.Equal(LogLine.Severity.Info, LogLine.SeverityOf("Device added: 'Soundbar'"));
        }

        [Fact]
        public void An_empty_status_list_is_still_reported()
        {
            var empty = "in [192.168.8.198:8009] [LoadingMedia]: {\"type\":\"MEDIA_STATUS\",\"status\":[],\"requestId\":0}";
            Assert.Contains("MEDIA_STATUS (empty)", LogLine.Format(empty, T));
        }

        [Fact]
        public void Our_own_messages_pass_through_untouched()
        {
            Assert.Contains("Device added: 'Soundbar' (192.168.8.198:8009).",
                            LogLine.Format("Device added: 'Soundbar' (192.168.8.198:8009).", T));
        }

        [Fact]
        public void Nothing_at_all_does_not_throw()
        {
            Assert.StartsWith("08:33:19.123", LogLine.Format(null, T));
            Assert.StartsWith("08:33:19.123", LogLine.Format("", T));
        }
    }
}
