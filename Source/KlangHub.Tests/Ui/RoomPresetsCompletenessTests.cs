using System.Globalization;
using System.Linq;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// Labels() silently falls back to English when a translation does not have exactly the expected number
    /// of entries - deliberately, because a shifted list would label the kitchen "Bathroom". The catch is
    /// that the fallback made the existing tests unfalsifiable: a language could lose an entry and every
    /// test still passed, with users quietly getting English room names. These tests check each shipped
    /// language on its own terms.
    /// </summary>
    public class RoomPresetsCompletenessTests
    {
        public static TheoryData<string> Cultures()
        {
            var data = new TheoryData<string>();
            foreach (var culture in KlangHub.MainForm.SupportedCultures)
                data.Add(culture);
            return data;
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void Every_language_carries_the_full_list_in_its_own_words(string culture)
        {
            var info = CultureInfo.GetCultureInfo(culture);
            var raw = KlangHub.Properties.Strings.ResourceManager.GetString("Room_Presets_Text", info);

            Assert.False(string.IsNullOrWhiteSpace(raw), culture + " has no room list at all");

            var entries = raw!.Split(';', System.StringSplitOptions.RemoveEmptyEntries)
                              .Select(p => p.Trim())
                              .Where(p => p.Length > 0)
                              .ToArray();

            // Counted against the resource itself, not against Labels() - Labels() would hand back English
            // and the assertion would pass while the user saw the wrong language.
            Assert.Equal(RoomPresets.Ids.Length, entries.Length);
            Assert.Equal(entries.Length, entries.Distinct(System.StringComparer.CurrentCultureIgnoreCase).Count());
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void And_that_list_is_the_one_actually_used(string culture)
        {
            var labels = RoomPresets.Labels(CultureInfo.GetCultureInfo(culture));
            Assert.Equal(RoomPresets.Ids.Length, labels.Length);
            Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        }

        [Fact]
        public void A_room_named_in_one_language_keeps_its_icon_in_another()
        {
            // The point of matching across every shipped language: the app was German when the room was
            // named, and English when the tile is drawn.
            var german = RoomPresets.Labels(CultureInfo.GetCultureInfo("de"));
            var english = RoomPresets.Labels(CultureInfo.GetCultureInfo("en"));

            for (int i = 0; i < RoomPresets.Ids.Length; i++)
            {
                Assert.Equal(RoomPresets.Ids[i], RoomPresets.IdFor(german[i]));
                Assert.Equal(RoomPresets.Ids[i], RoomPresets.IdFor(english[i]));
            }
        }

        [Fact]
        public void A_name_the_user_typed_is_not_forced_into_a_preset()
        {
            Assert.Null(RoomPresets.IdFor("the maintainer's Ecke"));
            Assert.Null(RoomPresets.IdFor("   "));
            Assert.Null(RoomPresets.IdFor(null));
        }
    }
}
