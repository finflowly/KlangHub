using System;
using KlangHub.Application;                 // IDevice
using KlangHub.Application.Interfaces;      // IDevices, IDeviceStatusTimer
using KlangHub.Application.Orchestration;   // Orchestrator
using KlangHub.Core.Audio;                  // AudioFrame, AudioFormat
using KlangHub.Core.Casting;                // ICastProvider, IPlaybackSession, CastDeviceDescriptor, ProviderId
using KlangHub.Core.Diagnostics;            // ILogger
using KlangHub.Core.Models;                 // SupportedStreamFormat
using KlangHub.Streaming.Interfaces;        // IStreamingRequestsListener
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace KlangHub.Tests.Orchestration
{
    public class OrchestratorTests
    {
        private static Orchestrator NewOrchestrator(IDevices? devices = null, ICastProvider? castProvider = null)
        {
            return new Orchestrator(
                devices ?? Substitute.For<IDevices>(),
                Substitute.For<IStreamingRequestsListener>(),
                castProvider ?? Substitute.For<ICastProvider>(),
                Substitute.For<IDeviceStatusTimer>(),
                Substitute.For<ILogger>());
        }

        [Fact]
        public void ResolveSession_returns_the_session_for_a_present_device()
        {
            var descriptor = new CastDeviceDescriptor("id", "Speaker", ProviderId.Chromecast, false);
            var device = Substitute.For<IDevice, IPlaybackSession>();
            ((IPlaybackSession)device).Device.Returns(descriptor);
            var session = Substitute.For<IPlaybackSession>();
            var castProvider = Substitute.For<ICastProvider>();
            castProvider.CreateSession(descriptor).Returns(session);

            var orch = NewOrchestrator(castProvider: castProvider);

            Assert.Same(session, orch.ResolveSession(device));
        }

        [Fact]
        public void ResolveSession_returns_null_when_the_device_has_left_the_registry()
        {
            var descriptor = new CastDeviceDescriptor("id", "Speaker", ProviderId.Chromecast, false);
            var device = Substitute.For<IDevice, IPlaybackSession>();
            ((IPlaybackSession)device).Device.Returns(descriptor);
            var castProvider = Substitute.For<ICastProvider>();
            castProvider.CreateSession(descriptor).Throws(new InvalidOperationException());

            var orch = NewOrchestrator(castProvider: castProvider);

            Assert.Null(orch.ResolveSession(device));
        }

        [Fact]
        public void SetStreamFormat_restarts_devices_only_when_the_format_changes()
        {
            var devices = Substitute.For<IDevices>();
            var orch = NewOrchestrator(devices: devices);

            orch.SetStreamFormat(SupportedStreamFormat.Wav);   // default Mp3_320 -> changes
            devices.Received(1).Stop();
            devices.Received(1).Start();

            devices.ClearReceivedCalls();
            orch.SetStreamFormat(SupportedStreamFormat.Wav);   // same -> no restart
            devices.DidNotReceive().Stop();
            devices.DidNotReceive().Start();
        }

        [Fact]
        public void DeviceRemoved_event_fires_when_the_devices_remove_callback_is_invoked()
        {
            var devices = Substitute.For<IDevices>();
            Action<IDevice>? removeCallback = null;
            devices.When(d => d.SetRemoveCallback(Arg.Any<Action<IDevice>>()))
                   .Do(ci => removeCallback = ci.Arg<Action<IDevice>>());

            var orch = NewOrchestrator(devices: devices);

            IDevice? removed = null;
            orch.DeviceRemoved += d => removed = d;
            var device = Substitute.For<IDevice>();
            removeCallback!(device);

            Assert.Same(device, removed);
        }

        [Fact]
        public void OnRecordingDataAvailable_forwards_wav_data_unencoded()
        {
            var devices = Substitute.For<IDevices>();
            var orch = NewOrchestrator(devices: devices);
            orch.SetStreamFormat(SupportedStreamFormat.Wav);
            devices.ClearReceivedCalls();

            var data = new byte[] { 1, 2, 3, 4 };
            orch.OnRecordingDataAvailable(new AudioFrame(data, 44100, 16, 2));

            devices.Received(1).OnRecordingDataAvailable(data, Arg.Any<AudioFormat>(), Arg.Any<int>(), SupportedStreamFormat.Wav);
        }
    }
}
