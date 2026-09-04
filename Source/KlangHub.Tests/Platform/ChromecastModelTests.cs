using KlangHub.Application;
using KlangHub.Discover;
using KlangHub.Platform.Casting.Chromecast;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// The card subtitle shows the hardware a room is built from, read out of the Chromecast mDNS TXT record.
    /// These pin the parsing down: real records arrive as one flat string of quoted key=value tokens, groups
    /// must stay unnamed (their "model" is the literal "Google Cast Group"), and anything missing degrades to
    /// null instead of leaking a fragment onto the card.
    /// </summary>
    public class ChromecastModelTests
    {
        [Fact]
        public void Reads_the_model_from_a_quoted_txt_record()
        {
            const string headers = "\"id=abc\" \"md=Google Nest Audio\" \"fn=Wohnzimmer\"";
            Assert.Equal("Google Nest Audio", ChromecastDeviceDiscovery.ModelOf(headers));
        }

        [Fact]
        public void Reads_the_model_when_it_is_the_last_token()
        {
            Assert.Equal("Chromecast Ultra", ChromecastDeviceDiscovery.ModelOf("id=abc;md=Chromecast Ultra"));
        }

        [Fact]
        public void Ignores_the_group_pseudo_model()
        {
            Assert.Null(ChromecastDeviceDiscovery.ModelOf("\"md=Google Cast Group\""));
        }

        [Fact]
        public void Prefers_the_eureka_product_name_over_the_txt_record()
        {
            var device = new DiscoveredDevice
            {
                Headers = "\"md=Google Cast Speaker\"",
                Eureka = new DeviceEureka { DeviceInfo = new DeviceInfo { Manufacturer = "Harman Kardon", Model_name = "Enchant" } },
            };
            Assert.Equal("Harman Kardon Enchant", device.ModelName);
        }

        [Fact]
        public void Does_not_repeat_a_manufacturer_the_model_already_names()
        {
            var device = new DiscoveredDevice
            {
                Eureka = new DeviceEureka { DeviceInfo = new DeviceInfo { Manufacturer = "Google", Model_name = "Google Nest Audio" } },
            };
            Assert.Equal("Google Nest Audio", device.ModelName);
        }

        [Fact]
        public void Falls_back_to_the_txt_record_until_eureka_answers()
        {
            var device = new DiscoveredDevice { Headers = "\"md=Chromecast Ultra\"" };
            Assert.Equal("Chromecast Ultra", device.ModelName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("\"id=abc\" \"fn=Kueche\"")]
        [InlineData("\"md=\"")]
        public void Returns_null_when_no_model_is_announced(string? headers)
        {
            Assert.Null(ChromecastDeviceDiscovery.ModelOf(headers));
        }
    }
}
