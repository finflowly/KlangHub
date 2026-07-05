using KlangHub.Communication;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class ChromeCastMessagesTests
    {
        [Theory]
        [InlineData("audio/wav")]
        [InlineData("audio/mpeg")]
        [InlineData("audio/flac")]
        public void GetLoadMessage_uses_the_supplied_content_type(string contentType)
        {
            var messages = new ChromeCastMessages();

            var msg = messages.GetLoadMessage("http://192.168.1.5:8080/", "client-1", "web-1", 42, "KlangHub", contentType);

            Assert.Contains($"\"contentType\":\"{contentType}\"", msg.PayloadUtf8);
        }
    }
}
