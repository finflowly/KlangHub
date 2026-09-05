using System.Globalization;
using System.Linq;
using KlangHub.Classes;
using KlangHub.Core.Models;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// What a fresh installation starts with, and how the settings page says so.
    /// <para>
    /// The word "default" used to live inside the translated <em>text</em> of one of the choices, in all
    /// twenty-four languages. Changing what the default is therefore meant editing twenty-four
    /// translations, and a label that has to be maintained by hand to stay true is a label that will
    /// eventually lie. The mark is now derived from the value, so it cannot say the wrong thing.
    /// </para>
    /// </summary>
    public class RecommendedDefaultsTests
    {
        [Fact]
        public void A_new_installation_streams_flac()
        {
            // Lossless, and compressed so it fits down a wireless link that uncompressed 24-bit can
            // overrun - which is what the receivers reported as a decode error.
            Assert.Equal(SupportedStreamFormat.Flac, RecommendedDefaults.StreamFormat);
        }

        [Fact]
        public void A_new_installation_keeps_ten_seconds_of_cushion()
            => Assert.Equal(10, RecommendedDefaults.ExtraBufferSeconds);

        [Fact]
        public void Only_the_recommended_choices_are_marked()
        {
            Assert.True(RecommendedDefaults.IsRecommended(SupportedStreamFormat.Flac));
            Assert.False(RecommendedDefaults.IsRecommended(SupportedStreamFormat.Wav_24bit));
            Assert.False(RecommendedDefaults.IsRecommended(SupportedStreamFormat.Mp3_128));

            Assert.True(RecommendedDefaults.IsRecommended(10));
            Assert.False(RecommendedDefaults.IsRecommended(0));
            Assert.False(RecommendedDefaults.IsRecommended(20));
        }

        [Fact]
        public void The_recommended_format_says_so_in_the_list()
        {
            var flac = new ComboboxItem(SupportedStreamFormat.Flac).ToString();
            var wav = new ComboboxItem(SupportedStreamFormat.Wav_24bit).ToString();

            Assert.Contains(Resource.Get("Option_Recommended_Text"), flac);
            Assert.DoesNotContain(Resource.Get("Option_Recommended_Text"), wav);
        }

        [Fact]
        public void The_recommended_buffer_says_so_in_the_list()
        {
            Assert.Contains(Resource.Get("Option_Recommended_Text"), new ComboboxItem(10).ToString());
            Assert.DoesNotContain(Resource.Get("Option_Recommended_Text"), new ComboboxItem(3).ToString());
        }

        [Fact]
        public void A_buffer_entry_still_reads_as_its_number()
        {
            // The list is read as seconds; whatever is added must not swallow the figure.
            Assert.StartsWith("10", new ComboboxItem(10).ToString());
            Assert.Equal("3", new ComboboxItem(3).ToString());
        }

        [Theory]
        [InlineData("en")] [InlineData("de")] [InlineData("fr")] [InlineData("es")] [InlineData("it")]
        [InlineData("nl")] [InlineData("pt")] [InlineData("pl")] [InlineData("sv")] [InlineData("da")]
        [InlineData("fi")] [InlineData("et")] [InlineData("lv")] [InlineData("lt")] [InlineData("el")]
        [InlineData("cs")] [InlineData("sk")] [InlineData("sl")] [InlineData("hr")] [InlineData("hu")]
        [InlineData("ro")] [InlineData("bg")] [InlineData("ga")] [InlineData("mt")]
        public void Every_language_has_the_mark(string culture)
        {
            var text = Resource.Get("Option_Recommended_Text", new CultureInfo(culture));

            Assert.False(string.IsNullOrWhiteSpace(text), $"no recommendation mark in '{culture}'");
        }

        [Theory]
        [InlineData("en")] [InlineData("de")] [InlineData("fr")] [InlineData("es")] [InlineData("it")]
        [InlineData("nl")] [InlineData("pt")] [InlineData("pl")] [InlineData("sv")] [InlineData("da")]
        [InlineData("fi")] [InlineData("et")] [InlineData("lv")] [InlineData("lt")] [InlineData("el")]
        [InlineData("cs")] [InlineData("sk")] [InlineData("sl")] [InlineData("hr")] [InlineData("hu")]
        [InlineData("ro")] [InlineData("bg")] [InlineData("ga")] [InlineData("mt")]
        public void No_format_label_still_calls_itself_the_default(string culture)
        {
            // The mark is derived from the value now. A label that also claims it in prose is one edit
            // away from contradicting the program.
            var suspects = new[] { "default", "Standard", "standaard", "standard", "predefinito",
                                   "por defecto", "padrão", "domyślnie", "oletus", "vaikimisi",
                                   "noklusējums", "numatyta", "προεπιλογή", "výchozí", "predvolené",
                                   "privzeto", "zadano", "implicit", "стандарт", "réamhshocrú" };

            foreach (var name in new[] { "Wav", "Wav_16bit", "Wav_24bit", "Wav_32bit", "Mp3_320", "Mp3_128", "Flac" })
            {
                var label = Resource.Get(name, new CultureInfo(culture)) ?? string.Empty;
                foreach (var suspect in suspects)
                    Assert.False(label.Contains(suspect, System.StringComparison.OrdinalIgnoreCase),
                        $"'{name}' in '{culture}' still says it is the default: {label}");
            }
        }
    }
}
