using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// The room list is one translated string per language whose entries line up with a fixed array of
    /// identities by POSITION - that is what keeps twenty rooms in twenty-four languages down to one resource
    /// line each. The price is that a translation with a missing or extra entry would silently shift every
    /// room by one and label the kitchen "Bathroom", so the counts are pinned here.
    /// </summary>
    public class RoomPresetsTests
    {
        [Fact]
        public void Every_language_lists_exactly_the_rooms_that_exist()
        {
            foreach (var code in KlangHub.MainForm.SupportedCultures)
            {
                var culture = CultureInfo.GetCultureInfo(code);
                var labels = RoomPresets.Labels(culture);

                Assert.Equal(RoomPresets.Ids.Length, labels.Length);
                Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l), $"empty room name in '{code}'"));
                Assert.Equal(labels.Length, labels.Distinct(StringComparer.CurrentCultureIgnoreCase).Count());
            }
        }

        [Fact]
        public void Translations_are_actually_translated()
        {
            var english = RoomPresets.Labels(CultureInfo.GetCultureInfo("en"));
            foreach (var code in new[] { "de", "fr", "es", "el", "bg", "fi" })
            {
                var labels = RoomPresets.Labels(CultureInfo.GetCultureInfo(code));
                // a language whose resource line is missing falls back to English - that is what this catches
                Assert.False(labels.SequenceEqual(english), $"'{code}' still shows the English room list");
            }
        }

        [Theory]
        [InlineData("de", "Wohnzimmer", "living")]
        [InlineData("de", "Küche", "kitchen")]
        [InlineData("en", "Bedroom", "bedroom")]
        [InlineData("fr", "Cuisine", "kitchen")]
        public void A_known_room_is_recognised_by_its_name(string culture, string name, string expectedId)
        {
            var previous = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(expectedId, RoomPresets.IdFor(name));
            }
            finally { Thread.CurrentThread.CurrentUICulture = previous; }
        }

        [Fact]
        public void A_room_keeps_its_identity_across_a_language_change()
        {
            // named while the app spoke German, still recognised (and still drawn with a sofa) in English
            var previous = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");
                Assert.Equal("living", RoomPresets.IdFor("Wohnzimmer"));
            }
            finally { Thread.CurrentThread.CurrentUICulture = previous; }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("the maintainer's Höhle")]
        public void A_room_typed_by_hand_has_no_preset_identity(string? name)
        {
            Assert.Null(RoomPresets.IdFor(name));
        }
    }
}
