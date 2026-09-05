using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KlangHub.Application;
using KlangHub.Communication;
using NSubstitute;
using Xunit;

namespace KlangHub.Tests.Application
{
    /// <summary>
    /// The fifteen-second status poll walks the device list while discovery is still adding to it.
    /// <para>
    /// Both happen on threads of their own - the poll on a <c>System.Timers.Timer</c>, the additions on
    /// whichever thread mDNS or the eureka_info reply arrives on - so the list really is touched from two
    /// places at once. Iterating it without a lock ends in
    /// <c>InvalidOperationException: Collection was modified</c>, which surfaces as a status poll that
    /// simply stops for that round.
    /// </para>
    /// <para>
    /// The list is reached by reflection because <c>Devices</c> has no way to be handed one: adding a
    /// device means constructing a real <see cref="Device"/> against a live application. The point being
    /// locked down is the locking, not the construction.
    /// </para>
    /// </summary>
    public class DevicesPollConcurrencyTests
    {
        private static List<IDevice> ListInside(Devices devices)
        {
            var field = typeof(Devices).GetField("deviceList", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (List<IDevice>)field!.GetValue(devices)!;
        }

        private static IDevice AQuietDevice()
        {
            var device = Substitute.For<IDevice>();
            // Anything but Disposed - the poll removes disposed devices before it walks the list.
            device.GetDeviceState().Returns(DeviceState.Idle);
            return device;
        }

        [Fact]
        public async Task Polling_survives_a_device_appearing_while_the_list_is_walked()
        {
            var devices = new Devices();
            var list = ListInside(devices);
            for (var i = 0; i < 200; i++)
                list.Add(AQuietDevice());

            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Exception? discoveryFailure = null;

            // Discovery's side of it, holding the same lock OnDeviceAvailable holds.
            var discovering = Task.Run(() =>
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        lock (list)
                        {
                            list.Add(AQuietDevice());
                            if (list.Count > 400)
                                list.RemoveAt(list.Count - 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    discoveryFailure = ex;
                }
            }, TestContext.Current.CancellationToken);

            var polls = 0;
            while (!stop.IsCancellationRequested && polls < 2000)
            {
                devices.OnGetStatus();
                polls++;
            }

            stop.Cancel();
            await discovering;

            Assert.Null(discoveryFailure);
            Assert.True(polls > 0, "the poll never ran");
        }
    }
}
