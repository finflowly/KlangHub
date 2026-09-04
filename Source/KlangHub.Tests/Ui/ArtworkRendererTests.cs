using System;
using System.Globalization;
using System.IO;
using System.Threading;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// The Chromecast screen is part of the product, so it has to speak the user's language too - a localized
    /// app that puts an English claim on the television is only half translated. These pin down that every
    /// shipped language produces its own artwork, and that switching languages actually changes the picture.
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

        [Theory]
        [InlineData("en")]
        [InlineData("de")]
        [InlineData("el")]   // non-latin script
        [InlineData("bg")]   // cyrillic
        [InlineData("ga")]
        [InlineData("mt")]
        public void Renders_a_png_for_every_shipped_script(string culture)
        {
            Use(culture);
            var png = ArtworkRenderer.CurrentPng();

            Assert.True(png.Length > 4096, $"artwork for '{culture}' is suspiciously small: {png.Length} bytes");
            // PNG signature - proves we produced an image, not the empty fallback
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        }

        [Fact]
        public void Every_supported_language_carries_its_own_tagline()
        {
            var english = Lookup("en");
            foreach (var code in KlangHub.MainForm.SupportedCultures)
            {
                var tagline = Lookup(code);
                Assert.False(string.IsNullOrWhiteSpace(tagline), $"'{code}' has no artwork tagline");
                if (code != "en")
                    Assert.False(tagline == english, $"'{code}' still falls back to the English tagline");
            }

            static string Lookup(string code) =>
                KlangHub.Properties.Strings.ResourceManager.GetString(
                    "Artwork_Tagline_Text", CultureInfo.GetCultureInfo(code)) ?? string.Empty;
        }

        [Fact]
        public void Switching_language_changes_the_picture()
        {
            Use("de");
            var german = ArtworkRenderer.CurrentPng();
            Use("el");
            var greek = ArtworkRenderer.CurrentPng();

            Assert.NotEqual(german, greek);
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
