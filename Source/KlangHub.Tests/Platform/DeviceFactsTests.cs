using System.Linq;
using System.Text.Json;
using KlangHub.Application;
using KlangHub.Core.Casting;
using KlangHub.Discover;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// The fixtures are the real TXT records read off this network on 2026-09-04, not invented ones - the
    /// point of these tests is that the four devices we actually cast to are described correctly.
    /// </summary>
    public class DeviceFactsTests
    {
        private const string Television =
            "id=46b3a763957f247a86f342faafb869bc;cd=EA26F643AEDBF3D371F2DAD3D32A4931;rm=B8448DB66E8D36F1;" +
            "ve=05;md=Smart TV Pro;ic=/setup/icon.png;fn=TCL TV;ca=264709;st=1;bs=FA8FEA13090E;nf=1;" +
            "ct=9AE4CE;rs=Casting: KlangHub";

        private const string Soundbar =
            "id=b66d62a40abc22dafc6b319c8347d54e;ve=05;md=Q995GD;fn=Soundbar;ca=199172;st=0;rs=";

        private const string GoogleHome =
            "id=f3e4e25123730434fa22f651407929ad;ve=05;md=Google Home Speaker;fn=Google Home;ca=198660;st=0;rs=";

        private static DiscoveredDevice Device(string txt, string ip = "192.168.8.1") => new()
        {
            Headers = JsonSerializer.Serialize(txt.Split(';')),
            IPAddress = ip,
            Port = 8009,
            Name = "test",
        };

        private static string? Value(DiscoveredDevice d, DeviceFactKind kind)
            => DeviceFacts.For(d).Where(f => f.Kind == kind).Select(f => f.Value).FirstOrDefault();

        [Fact]
        public void Reads_every_key_of_the_announcement()
        {
            var txt = CastTxt.Parse(Device(Television).Headers);
            Assert.Equal("Smart TV Pro", txt["md"]);
            Assert.Equal("TCL TV", txt["fn"]);
            Assert.Equal("264709", txt["ca"]);
            Assert.Equal("Casting: KlangHub", txt["rs"]);
        }

        [Fact]
        public void A_value_containing_an_equals_sign_survives()
        {
            Assert.Equal("a=b", CastTxt.Value("[\"rs=a=b\"]", "rs"));
        }

        [Fact]
        public void Video_out_is_what_separates_a_screen_from_a_speaker()
        {
            Assert.Equal("tv", Value(Device(Television), DeviceFactKind.Type));
            Assert.Equal("speaker", Value(Device(Soundbar), DeviceFactKind.Type));
            Assert.Equal("speaker", Value(Device(GoogleHome), DeviceFactKind.Type));
        }

        [Fact]
        public void The_model_comes_from_the_announcement_because_nothing_else_carries_it()
        {
            Assert.Equal("Q995GD", Value(Device(Soundbar), DeviceFactKind.Model));
            Assert.Equal("Google Home Speaker", Value(Device(GoogleHome), DeviceFactKind.Model));
        }

        [Fact]
        public void Undocumented_capability_bits_are_kept_as_a_number_not_guessed_at()
        {
            Assert.True(CastCapabilities.Has("264709", CastCapability.VideoOut));
            Assert.True(CastCapabilities.Has("264709", CastCapability.AudioOut));
            Assert.False(CastCapabilities.Has("198660", CastCapability.VideoOut));
            Assert.NotEqual(0, CastCapabilities.UndocumentedBits("198660"));
        }

        [Fact]
        public void A_placeholder_mac_is_not_shown_as_an_address()
        {
            var device = Device(Soundbar);
            device.MACAddress = "00:00:00:00:00:00";
            Assert.Null(Value(device, DeviceFactKind.MacAddress));
        }

        [Fact]
        public void Facts_that_have_no_source_are_left_out_entirely()
        {
            var facts = DeviceFacts.For(Device(Soundbar));
            Assert.DoesNotContain(facts, f => f.Kind == DeviceFactKind.Manufacturer);
            Assert.DoesNotContain(facts, f => f.Kind == DeviceFactKind.Firmware);
            Assert.All(facts, f => Assert.False(string.IsNullOrWhiteSpace(f.Value)));
        }

        [Fact]
        public void An_empty_activity_string_does_not_become_an_empty_row()
        {
            Assert.Null(Value(Device(Soundbar), DeviceFactKind.Activity));
            Assert.Equal("Casting: KlangHub", Value(Device(Television), DeviceFactKind.Activity));
        }

        [Fact]
        public void A_device_with_no_announcement_at_all_yields_no_facts_rather_than_throwing()
        {
            Assert.Empty(DeviceFacts.For(null));
            Assert.Empty(CastTxt.Parse(null));
        }
    }
}
