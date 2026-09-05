using KlangHub.Core.Models;

namespace KlangHub.Classes
{
    /// <summary>
    /// One entry in a settings list: the value it stands for, and the text the user reads.
    /// </summary>
    public class ComboboxItem
    {
        public object Value { get; set; }

        public ComboboxItem(object value)
        {
            Value = value;
        }

        /// <summary>
        /// Return the text, translate the supported enums, and mark the entry a fresh installation gets.
        /// <para>
        /// The mark is derived from the value, never written into the translation. It used to be prose -
        /// "WAV · 24-bit HiFi (uncompressed, default)" - in all twenty-four languages, which meant that
        /// changing the default was a translation job, and that the day somebody changed it without doing
        /// that job, the list would quietly say the wrong thing in every language at once.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            var text = Value is SupportedStreamFormat || Value is FilterDevicesEnum
                ? Resource.Get(Value.ToString()!)
                : Value.ToString()!;

            return IsRecommended() ? text + " " + Resource.Get("Option_Recommended_Text") : text;
        }

        private bool IsRecommended() => Value switch
        {
            SupportedStreamFormat format => RecommendedDefaults.IsRecommended(format),
            int seconds => RecommendedDefaults.IsRecommended(seconds),
            _ => false,
        };
    }
}
