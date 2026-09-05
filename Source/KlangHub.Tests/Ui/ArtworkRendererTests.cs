using System;
using System.Globalization;
using System.IO;
using System.Threading;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// The artwork is the picture the television shows. It carries two things and both are wordmarks:
    /// "KlangHub" and the claim beneath it. Neither is translated - a claim that changes wording per
    /// country is not a claim, it is a caption, and a listener who switches the app to Dutch has not
    /// asked for a different product name.
    /// <para>
    /// This replaced a rule that required the opposite. The tagline used to be translated into all
    /// twenty-four languages and a test held each language to a distinct wording; it was the ONLY
    /// localized text on the artwork, so the picture also differed per language and a second test
    /// pinned that down. Both premises are gone with the decision, and a test whose premise is gone is
    /// removed rather than rewritten into something it can still pass.
    /// </para>
    /// </summary>
    public class ArtworkRendererTests : IDisposable
    {
        private readonly CultureInfo original = Thread.CurrentThread.CurrentUICulture;

        public void Dispose()
        {
            Thread.CurrentThread.CurrentUICulture = original;
            ArtworkRenderer.Invalidate();
        }

        private static void Use(string culture)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            ArtworkRenderer.Invalidate();
        }

        /// <summary>
        /// The renderer still produces a real picture whatever the app language is. The cultures here are no
        /// longer about scripts - the artwork carries no translated text any more - but the renderer reads a
        /// resource through a culture, and a language that broke that lookup would leave the television with
        /// the empty fallback rather than an error.
        /// </summary>
        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        [InlineData("el")]
        [InlineData("bg")]
        [InlineData("ga")]
        [InlineData("mt")]
        public void Renders_a_png_whatever_the_app_language_is(string culture)
        {
            Use(culture);
            var png = ArtworkRenderer.CurrentPng();

            Assert.True(png.Length > 4096, $"artwork for '{culture}' is suspiciously small: {png.Length} bytes");
            // PNG signature - proves we produced an image, not the empty fallback
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        }

        /// <summary>The claim is a wordmark: the same words in every language, and present in every one.</summary>
        [Fact]
        public void The_claim_is_the_same_wordmark_in_every_language()
        {
            const string wordmark = "ONE MUSIC · EVERY ROOM";

            foreach (var code in KlangHub.MainForm.SupportedCultures)
                Assert.Equal(wordmark, Lookup(code));

            static string Lookup(string code) =>
                KlangHub.Properties.Strings.ResourceManager.GetString(
                    "Artwork_Tagline_Text", CultureInfo.GetCultureInfo(code)) ?? string.Empty;
        }

        /// <summary>
        /// Every language must still HAVE the key. Deleting it from a file would fall back to the neutral
        /// resource and look identical - the test above would stay green while one language quietly stopped
        /// shipping its own copy, and the next translated string added to the artwork would then be missing.
        /// </summary>
        [Fact]
        public void No_language_has_dropped_the_key()
        {
            foreach (var code in KlangHub.MainForm.SupportedCultures)
            {
                if (code == "en")
                    continue;

                var culture = CultureInfo.GetCultureInfo(code);
                using var set = KlangHub.Properties.Strings.ResourceManager.GetResourceSet(culture, true, false);
                Assert.True(set?.GetString("Artwork_Tagline_Text") != null,
                    $"'{code}' has no Artwork_Tagline_Text of its own and is falling back");
            }
        }

        [Fact]
        public void Follows_the_app_language_even_on_a_background_thread()
        {
            // The regression this pins down: the streaming server answers on its own thread, which never saw
            // the UI thread's culture - so the television kept showing the language of the first cast while
            // the app itself had already switched. The app-wide default is what the renderer must read.
            var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("de");
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("nl");
                ArtworkRenderer.Invalidate();

                string? seen = null;
                byte[]? png = null;
                var worker = new Thread(() =>
                {
                    seen = ArtworkRenderer.Culture.TwoLetterISOLanguageName;
                    png = ArtworkRenderer.CurrentPng();
                });
                worker.Start();
                worker.Join();

                // The culture the renderer resolves is the fact under test. It used to be checked a second way,
                // by rendering German afterwards and requiring a different picture - which worked only while
                // the tagline was translated and was the sole localized text on the artwork. Both pictures are
                // identical now by design, so that assertion would have to be deleted or faked; it is deleted.
                Assert.Equal("nl", seen);
                Assert.NotNull(png);
            }
            finally
            {
                CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
                ArtworkRenderer.Invalidate();
            }
        }

        [Fact]
        public void The_same_language_is_rendered_once_and_reused()
        {
            Use("de");
            var first = ArtworkRenderer.CurrentPng();
            var second = ArtworkRenderer.CurrentPng();

            Assert.Same(first, second);
        }
    }
}
