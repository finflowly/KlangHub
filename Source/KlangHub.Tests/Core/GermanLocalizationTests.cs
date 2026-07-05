using System.Globalization;
using Xunit;

namespace KlangHub.Tests.Core
{
    // Verifies the German satellite resources (Strings.de.resx) build and resolve at runtime, so the app can
    // offer German + default to it when the OS is German.
    public class GermanLocalizationTests
    {
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de");

        [Theory]
        [InlineData("Playing", "Wiedergabe")]
        [InlineData("Tab_Main_Text", "Räume")]         // HiFi copy pass: "Geräte" -> "Räume"
        [InlineData("Language", "Deutsch")]            // the language's self-name (drives the picker item)
        [InlineData("Button_ScanAgain_Text", "Räume neu suchen")]
        [InlineData("Group_Options_Text", "Einstellungen")]
        public void German_strings_resolve(string key, string expected)
        {
            Assert.Equal(expected, KlangHub.Properties.Strings.ResourceManager.GetString(key, De));
        }
    }
}
