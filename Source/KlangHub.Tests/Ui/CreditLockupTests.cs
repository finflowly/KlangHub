using System.Globalization;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// The credit line in the footer is a lockup, not a sentence: the same English words in every
    /// language, like the artwork wordmark. It used to be translated - "In der Matrix entwickelt von",
    /// "Développé dans la Matrice par" - which reads as a caption written about the authors rather than
    /// as their signature.
    /// <para>
    /// This is one of exactly two English lockups in an application that is otherwise fully translated
    /// into all twenty-four EU languages; the other is <c>Artwork_Tagline_Text</c>. Everything else stays
    /// localized, and a future string is translated unless someone deliberately adds it here.
    /// </para>
    /// </summary>
    public class CreditLockupTests
    {
        private const string Credit = "Developed in the Matrix by Neo & Trinity · 2026";

        [Fact]
        public void The_credit_is_the_same_english_line_in_every_language()
        {
            foreach (var code in KlangHub.MainForm.SupportedCultures)
                Assert.Equal(Credit, LookupCredit(code));
        }

        /// <summary>
        /// The ampersand must arrive as "&amp;". It is written "&amp;amp;" in the resx because the file is
        /// XML, and a lookup that handed the escape through to a Label would put the escape on screen.
        /// The Label also sets UseMnemonic = false, or WinForms would eat the ampersand as an accelerator
        /// and render "Neo  Trinity".
        /// </summary>
        [Fact]
        public void The_ampersand_survives_the_resource_lookup()
        {
            Assert.Contains("Neo & Trinity", LookupCredit("en"));
            Assert.DoesNotContain("&amp;", LookupCredit("en"));
        }

        /// <summary>
        /// Every language must still carry the key rather than fall back to the neutral resource. With all
        /// values identical a dropped key is invisible - see the same guard on the artwork wordmark.
        /// </summary>
        [Fact]
        public void No_language_has_dropped_the_credit()
        {
            foreach (var code in KlangHub.MainForm.SupportedCultures)
            {
                if (code == "en")
                    continue;

                var culture = CultureInfo.GetCultureInfo(code);

                // Not disposed - the set belongs to the ResourceManager and is cached; see the same note in
                // ArtworkRendererTests.
                var set = KlangHub.Properties.Strings.ResourceManager.GetResourceSet(culture, true, false);
                Assert.True(set?.GetString("Label_Credit_Text") != null,
                    $"'{code}' has no Label_Credit_Text of its own and is falling back");
            }
        }

        private static string LookupCredit(string code) =>
            KlangHub.Properties.Strings.ResourceManager.GetString(
                "Label_Credit_Text", CultureInfo.GetCultureInfo(code)) ?? string.Empty;
    }
}
