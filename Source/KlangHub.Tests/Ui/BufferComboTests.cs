using System.Linq;
using System.Windows.Forms;
using KlangHub.Application;
using KlangHub.Application.Interfaces;
using KlangHub.Classes;
using KlangHub.Core.Casting;
using KlangHub.Core.Models;
using KlangHub.Core.Audio;
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Ui
{
    /// <summary>
    /// The sound-buffer list on the settings page showed nothing at all, while every other list on the same
    /// card showed its value.
    /// <para>
    /// The effective buffer was right - ten seconds reached the stream - so this was only ever the field.
    /// But a settings page that cannot say what it is set to is a settings page nobody can trust, and the
    /// next person to look would have gone hunting in the audio path, which was never involved.
    /// </para>
    /// <para>
    /// The cause is an ordering one. <c>MainForm_Load</c> calls <c>applicationLogic.Initialize()</c>, which
    /// pushes every stored setting into the form, and only afterwards calls the Fill methods that create the
    /// list entries. <c>SetStreamFormat</c> and <c>SetFilterDevices</c> survive that by filling their own
    /// list first; <c>SetExtraBufferInSeconds</c> did not, so it looped over an empty list, selected nothing,
    /// and the entries were added a moment later with no selection on them. A DropDownList with items and
    /// SelectedIndex -1 draws an empty field.
    /// </para>
    /// </summary>
    public class BufferComboTests
    {
        private static MainForm NewForm() => new MainForm(
            Substitute.For<IApplicationLogic>(),
            Substitute.For<IDevices>(),
            Substitute.For<IAudioCaptureEngine>(),
            Substitute.For<KlangHub.Core.Diagnostics.ILogger>(),
            Substitute.For<ICastProvider>());

        private static ComboBox BufferCombo(MainForm form) =>
            (ComboBox)form.Controls.Find("cmbBufferInSeconds", searchAllChildren: true).Single();

        /// <summary>
        /// The order the form itself uses: settings are applied BEFORE the lists are filled. This is the
        /// test that was red - nothing here clicks anything, it just asks the setter to do its job at the
        /// moment the application actually asks it.
        /// </summary>
        [Fact]
        public void The_default_is_visible_when_the_setting_is_applied_before_the_lists_are_filled()
        {
            using var form = NewForm();

            form.SetExtraBufferInSeconds(RecommendedDefaults.ExtraBufferSeconds);

            var combo = BufferCombo(form);
            Assert.NotEmpty(combo.Items.Cast<object>());
            Assert.NotEqual(-1, combo.SelectedIndex);
            Assert.NotNull(combo.SelectedItem);

            // Not just an index: a DropDownList shows the item's text, and an empty text is the whole bug.
            Assert.False(string.IsNullOrWhiteSpace(combo.SelectedItem!.ToString()),
                "the buffer field would be blank on screen");
            Assert.Contains("10", combo.SelectedItem!.ToString()!);

            Assert.Equal(RecommendedDefaults.ExtraBufferSeconds, form.GetExtraBufferInSeconds());
        }

        /// <summary>A stored value that is in the list becomes visible too, not only the default.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(20)]
        public void A_stored_value_shows_up(int seconds)
        {
            using var form = NewForm();

            form.SetExtraBufferInSeconds(seconds);

            var combo = BufferCombo(form);
            Assert.NotEqual(-1, combo.SelectedIndex);
            Assert.Equal(seconds, form.GetExtraBufferInSeconds());
            Assert.Contains(seconds.ToString(), combo.SelectedItem!.ToString()!);
        }

        /// <summary>
        /// Setting it twice must not add the list twice. FillBufferSeconds is guarded on Items.Count, and
        /// the guard is what makes it safe to call from the setter as the other two setters do.
        /// </summary>
        [Fact]
        public void Applying_the_setting_repeatedly_does_not_grow_the_list()
        {
            using var form = NewForm();

            form.SetExtraBufferInSeconds(10);
            var afterFirst = BufferCombo(form).Items.Count;
            form.SetExtraBufferInSeconds(5);
            form.SetExtraBufferInSeconds(10);

            Assert.Equal(afterFirst, BufferCombo(form).Items.Count);
            Assert.Equal(10, form.GetExtraBufferInSeconds());
        }

        /// <summary>
        /// Switching language must not blank the field. Nothing clears this list on a culture change - only
        /// the address list is rebuilt - but the entry's text is produced by ComboboxItem.ToString(), which
        /// reads a translated word for the "recommended" mark, so the assumption is worth holding down.
        /// </summary>
        [Fact]
        public void Switching_language_leaves_the_value_visible()
        {
            using var form = NewForm();
            form.SetExtraBufferInSeconds(10);
            var before = BufferCombo(form).SelectedIndex;

            form.SetCulture("de");
            form.SetCulture("en");

            var combo = BufferCombo(form);
            Assert.Equal(before, combo.SelectedIndex);
            Assert.NotNull(combo.SelectedItem);
            Assert.False(string.IsNullOrWhiteSpace(combo.SelectedItem!.ToString()));
            Assert.Equal(10, form.GetExtraBufferInSeconds());
        }

        /// <summary>
        /// The list holds the steps that were always there: 0 to 20 seconds, one entry each. Pinned so a
        /// later "tidy-up" cannot quietly drop or invent a step.
        /// </summary>
        [Fact]
        public void The_list_holds_the_existing_steps_and_no_invented_ones()
        {
            using var form = NewForm();
            form.SetExtraBufferInSeconds(10);

            var values = BufferCombo(form).Items.Cast<ComboboxItem>()
                .Select(i => (int)i.Value)
                .ToArray();

            Assert.Equal(Enumerable.Range(0, 21).ToArray(), values);
        }
    }
}
