using System;
using KlangHub.Core.Diagnostics;
using Xunit;

namespace KlangHub.Tests.Core
{
    /// <summary>
    /// The receiver error that ended a stream on 2026-09-05 at 08:45:30, and what a reader needs from it.
    /// </summary>
    public class LogLineErrorTests
    {
        private static readonly DateTime T = new(2026, 9, 5, 8, 45, 30, 423, DateTimeKind.Local);

        private const string ReceiverError =
            "in [192.168.8.198:8009] [Playing]: {\"type\":\"ERROR\",\"requestId\":0,\"detailedErrorCode\":102,\"itemId\":1}";

        [Fact]
        public void An_error_from_the_receiver_counts_as_a_failure()
        {
            Assert.Equal(LogLine.Severity.Error, LogLine.SeverityOf(ReceiverError));
            Assert.Contains(" ERR ", LogLine.Format(ReceiverError, T));
        }

        [Fact]
        public void The_error_code_is_spelled_out_rather_than_left_as_a_number()
        {
            var line = LogLine.Format(ReceiverError, T);
            Assert.Contains("detailedErrorCode\":102", line);
            Assert.Contains("could not decode the stream", line);
        }

        [Fact]
        public void The_codes_that_actually_occur_all_have_an_explanation()
        {
            foreach (var code in new[] { 100, 101, 102, 103, 104, 110, 905, 906 })
                Assert.False(string.IsNullOrWhiteSpace(LogLine.ExplainErrorCode(code)), "code " + code);
        }

        [Fact]
        public void A_code_we_do_not_know_is_not_invented()
        {
            Assert.Null(LogLine.ExplainErrorCode(4711));

            var unknown = "in [x] [Playing]: {\"type\":\"ERROR\",\"detailedErrorCode\":4711}";
            Assert.DoesNotContain("<-", LogLine.Format(unknown, T));
        }
    }
}
