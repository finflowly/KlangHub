using System;
using System.Reflection;
using KlangHub.Application;
using KlangHub.Communication;
using KlangHub.Communication.Classes;
using KlangHub.Communication.Interfaces;
using KlangHub.Core.Diagnostics;
using KlangHub.Core.Casting;
using KlangHub.Classes;
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// What a device does with a volume change, and what it does with the answer.
    /// <para>
    /// This is the other half of the SET_VOLUME loop - <see cref="VolumeLevel"/> holds the arithmetic,
    /// this holds the decisions around it. Two devices were seen reporting <c>0.13999999</c> and
    /// <c>0.20000005</c> with <c>controlType: "fixed"</c> and being sent another SET_VOLUME each time.
    /// A fixed-volume device is one that cannot accept the change at all, so nothing it reports back will
    /// ever match, and <c>controlType</c> was read from the wire and then never looked at anywhere in the
    /// project.
    /// </para>
    /// <para>
    /// The device's collaborators are replaced through the fields rather than the constructor: it builds
    /// its own connection, and the point being tested is the volume decision, not the wiring.
    /// </para>
    /// </summary>
    public class DeviceVolumeTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 9, 5, 20, 0, 0, DateTimeKind.Utc);

        private static (Device device, IDeviceCommunication sent) ADevice(string controlType, float step, float level = 0.5f)
        {
            var device = new Device(Substitute.For<ILogger>(), Substitute.For<ICastHost>());

            var sent = Substitute.For<IDeviceCommunication>();
            typeof(Device)
                .GetField("deviceCommunication", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(device, sent);

            device.OnVolumeUpdate(new Volume { controlType = controlType, level = level, muted = false, stepInterval = step }, T0);
            sent.ClearReceivedCalls();
            return (device, sent);
        }

        [Fact]
        public void A_device_whose_volume_is_fixed_is_never_asked_to_change_it()
        {
            // The whole loop in one test. A television on a fixed output reports its level and cannot
            // take a new one; asking anyway produced a SET_VOLUME for every status message it sent.
            var (device, sent) = ADevice("fixed", step: 0f, level: 0.14f);

            device.VolumeSet(0.5f, T0.AddSeconds(1));

            sent.DidNotReceive().VolumeSet(Arg.Any<Volume>());
        }

        [Fact]
        public void A_device_that_can_change_its_volume_is_asked_to()
        {
            var (device, sent) = ADevice("attenuation", step: 0.05f);

            device.VolumeSet(0.5f, T0.AddSeconds(1));

            sent.Received(1).VolumeSet(Arg.Is<Volume>(v => VolumeLevel.Same(v.level, 0.5f)));
        }

        [Fact]
        public void The_level_sent_is_snapped_to_the_step_the_device_works_in()
        {
            // Snapped in one rounding. Reaching it by adding 0.02 to itself seven times is what produced
            // 0.13999999 and started the argument.
            var (device, sent) = ADevice("attenuation", step: 0.02f, level: 0f);

            device.VolumeSet(0.138f, T0.AddSeconds(1));

            sent.Received(1).VolumeSet(Arg.Is<Volume>(v => VolumeLevel.Same(v.level, 0.14f)));
        }

        [Fact]
        public void A_reported_level_a_rounding_error_away_is_taken_as_agreement()
        {
            var (device, _) = ADevice("attenuation", step: 0.02f, level: 0f);
            device.VolumeSet(0.14f, T0.AddSeconds(1));

            // The device answers with what a float can manage.
            VolumeStatus? seen = null;
            device.VolumeChanged += (_, v) => seen = v;
            device.OnVolumeUpdate(
                new Volume { controlType = "attenuation", level = 0.13999999f, muted = false, stepInterval = 0.02f },
                T0.AddSeconds(2));

            Assert.NotNull(seen);
            Assert.True(VolumeLevel.Same(seen!.Value.Level, 0.14f));
            Assert.False(HasPending(device), "the device agreed - nothing should still be outstanding");
        }

        [Fact]
        public void A_device_that_keeps_disagreeing_is_eventually_believed()
        {
            // Otherwise the card shows a level the speaker is not at, for as long as it runs.
            var (device, _) = ADevice("attenuation", step: 0.05f, level: 0.2f);
            device.VolumeSet(0.8f, T0.AddSeconds(1));

            var stubborn = new Volume { controlType = "attenuation", level = 0.2f, muted = false, stepInterval = 0.05f };
            device.OnVolumeUpdate(stubborn, T0.AddSeconds(2));
            Assert.True(HasPending(device), "one disagreement is not yet a reason to give up");

            device.OnVolumeUpdate(stubborn, T0.AddSeconds(1) + Device.PendingVolumePatience.Add(TimeSpan.FromSeconds(1)));
            Assert.False(HasPending(device), "after the patience window the device's own level is the truth");
        }

        [Fact]
        public void Volume_up_and_down_move_by_the_step_the_device_reports()
        {
            // 0.05 was hard-coded, so on a television that moves in 1 % one press jumped five.
            var (device, sent) = ADevice("attenuation", step: 0.01f, level: 0.50f);

            device.VolumeUp(T0.AddSeconds(1));

            sent.Received(1).VolumeSet(Arg.Is<Volume>(v => VolumeLevel.Same(v.level, 0.51f)));
        }

        [Fact]
        public void A_fixed_device_reports_itself_as_fixed_to_the_card()
        {
            // So the card can grey its slider out instead of offering something that cannot work.
            var (device, _) = ADevice("fixed", step: 0f, level: 0.14f);

            VolumeStatus? seen = null;
            device.VolumeChanged += (_, v) => seen = v;
            device.OnVolumeUpdate(new Volume { controlType = "fixed", level = 0.14f, muted = false, stepInterval = 0f }, T0.AddSeconds(1));

            Assert.NotNull(seen);
            Assert.True(seen!.Value.IsFixed);
        }

        private static bool HasPending(Device device)
            => typeof(Device).GetField("pendingVolume", BindingFlags.NonPublic | BindingFlags.Instance)!
                   .GetValue(device) is float;
    }
}
