using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KlangHub.UserControls;
using Xunit;

namespace KlangHub.Tests.UserControls
{
    /// <summary>
    /// The amber chevron has to sit on the button that actually opens the list.
    /// <para>
    /// It did not. The chevron was centred on a button rectangle the control worked out for itself from a
    /// fixed width of 30 pixels - which is roughly right for a <c>DropDownList</c>, the shape the settings
    /// page uses. The room picker in the speaker popup is an <em>editable</em> combo, and Windows draws
    /// that button seventeen pixels wide. Measured on a 240-pixel field: the OS button runs from 221 to
    /// 238, the edit control from 3 to 220, and the chevron was painted around 219 - inside the text box,
    /// two pixels short of the button.
    /// </para>
    /// <para>
    /// So clicking the arrow put the caret in the text and selected the room name, and the list never
    /// appeared. Nothing was broken about the list: the arrow was simply not the button.
    /// </para>
    /// </summary>
    public class DarkComboBoxChevronTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public int stateButton;
            public IntPtr hwndCombo, hwndItem, hwndList;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO info);

        private static (DarkComboBox box, Form host) AnEditableCombo()
        {
            var host = new Form { ClientSize = new System.Drawing.Size(300, 80) };
            var box = new DarkComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Bounds = new System.Drawing.Rectangle(2, 3, 240, 30),
            };
            box.Items.AddRange(new object[] { "Living room", "Kitchen" });
            host.Controls.Add(box);
            host.CreateControl();
            _ = box.Handle;
            return (box, host);
        }

        [Fact]
        public void The_chevron_is_drawn_on_the_button_that_opens_the_list()
        {
            var (box, host) = AnEditableCombo();
            using (host)
            {
                var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
                Assert.True(GetComboBoxInfo(box.Handle, ref info), "could not ask Windows where its button is");

                var centre = box.DropDownButtonBounds;
                var mid = centre.Left + centre.Width / 2;

                Assert.True(mid >= info.rcButton.left && mid < info.rcButton.right,
                    $"the chevron is drawn at x={mid}, but the button Windows will accept a click on runs "
                    + $"from {info.rcButton.left} to {info.rcButton.right}. Clicking the arrow lands in the "
                    + "text box instead of opening the list.");
            }
        }

        [Fact]
        public void The_painted_band_covers_the_whole_of_the_native_button()
        {
            // Whatever the OS paints there is light, and has to be hidden completely - a leftover edge
            // shows as a stray pale sliver against the dark field.
            var (box, host) = AnEditableCombo();
            using (host)
            {
                var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
                Assert.True(GetComboBoxInfo(box.Handle, ref info));

                var band = box.DropDownButtonBounds;
                Assert.True(band.Left <= info.rcButton.left, $"band starts at {band.Left}, button at {info.rcButton.left}");
                Assert.True(band.Right >= info.rcButton.right, $"band ends at {band.Right}, button at {info.rcButton.right}");
            }
        }

        [Fact]
        public void A_closed_list_combo_still_gets_a_sensible_band()
        {
            // The settings page uses DropDownList, whose button Windows draws differently. The band must
            // stay on the right-hand side and inside the control either way.
            using var host = new Form { ClientSize = new System.Drawing.Size(300, 80) };
            var box = new DarkComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Bounds = new System.Drawing.Rectangle(2, 3, 240, 30),
            };
            host.Controls.Add(box);
            host.CreateControl();
            _ = box.Handle;

            var band = box.DropDownButtonBounds;
            Assert.True(band.Right <= box.Width, $"band runs past the control: {band} in a {box.Width}px field");
            Assert.True(band.Left > box.Width / 2, $"band should be on the right: {band}");
            Assert.True(band.Width > 0 && band.Height > 0, $"empty band: {band}");
        }

        [Fact]
        public void A_combo_without_a_window_yet_still_answers()
        {
            // OnPaint can run before the handle exists on some layout paths; asking Windows then is not an
            // option, so there has to be a sane answer without it.
            using var box = new DarkComboBox { Bounds = new System.Drawing.Rectangle(0, 0, 240, 30) };

            var band = box.DropDownButtonBounds;

            Assert.True(band.Width > 0 && band.Height > 0, $"empty band: {band}");
            Assert.True(band.Right <= 240, $"band runs past the control: {band}");
        }
    }
}
