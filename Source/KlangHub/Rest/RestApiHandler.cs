using KlangHub.Application;
using KlangHub.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KlangHub.Rest
{
    public static class RestApiHandler
    {
        // Bodies and framing are built in RestPayload, which escapes what it is given and counts the
        // length in bytes. Both used to happen here, by concatenation, and both were wrong for it.
        private static readonly string errorBadRequest = RestPayload.Error("400 Bad Request", "1");
        private static readonly string errorNotSupported = RestPayload.Error("400 Bad Request", "2", "Action not supported");
        private static readonly string errorDeviceNotFound = RestPayload.Error("404 Not Found", "3", "Device not found");
        private static readonly string errorWrongVolume = RestPayload.Error("400 Bad Request", "3", "Volume should be an integer between 0 and 100 (/volume/<device>/<volume>)");
        
        public static void Process(Socket socket, string request, IDevices devices, ILogger logger, Action restartRecording,
            Func<IDevice, IPlaybackSession> resolveSession)
        {
            if (request == null || socket == null)
                return;

            string response;
            try
            {
                var req = request.Split('\r');
                if (req.Length == 0)
                    return;

                var requestAction = req[0].Split(' ');
                if (requestAction.Length < 2 || string.IsNullOrEmpty(requestAction[1]))
                    return;

                var action = WebUtility.UrlDecode(requestAction[1].ToLowerInvariant());
                logger.Log($"RestApiHandler: {action}");
                if (action.StartsWith("/start"))
                    response = Start(action.Replace("/start", ""), devices, resolveSession);
                else if (action.StartsWith("/stop"))
                    response = Stop(action.Replace("/stop", ""), devices, resolveSession);
                else if (action.StartsWith("/volume"))
                    response = Volume(action.Replace("/volume", ""), devices, resolveSession);
                else if (action.StartsWith("/togglemute"))
                    response = ToggleMute(action.Replace("/togglemute", ""), devices, resolveSession);
                else if (action.StartsWith("/list"))
                    response = List(devices);
                else if (action.StartsWith("/restartrecording"))
                    response = RestartRecording(restartRecording);
                else
                    response = errorNotSupported;

                socket.Send(RestPayload.Http(ok: !response.StartsWith("{\"errors"), response));
            }
            catch (Exception ex)
            {
                logger.Log(ex.Message);
                socket.Send(RestPayload.Http(ok: false, errorBadRequest));
            }
        }

        private static string RestartRecording(Action restartRecording)
        {
            restartRecording?.Invoke();
            return RestPayload.Done("/restartrecording");
        }

        private static string List(IDevices devices)
        {
            // The friendly name is whatever the device announced over mDNS: data, not syntax.
            return RestPayload.DeviceList(devices.GetDeviceList().Select(device => new RestDevice(
                device.GetFriendlyName(),
                device.GetDeviceState().ToString(),
                device.GetVolumeLevel().ToString(),
                device.GetHost(),
                device.GetPort().ToString(),
                device.IsGroup().ToString())));
        }

        private static string ToggleMute(string action, IDevices devices, Func<IDevice, IPlaybackSession> resolveSession)
        {
            // 2.2b-4.4f: SetMuted(!Volume.Muted) mirrors device.VolumeMute()'s toggle. Accepted edge (as in
            // DeviceControl 4.4b): before the first volume update Volume.Muted defaults to false, so this
            // sends unmute->mute where the old VolumeMute() no-op'd on a null volumeSetting.
            if (string.IsNullOrEmpty(action.Replace("/", "")))
            {
                foreach (var device in devices.GetDeviceList())
                    Control(device, resolveSession, s => s.SetMuted(!s.Volume.Muted), d => d.VolumeMute());
            }
            else
            {
                var device = GetDevice(devices, action);
                if (device == null)
                    return errorDeviceNotFound;

                Control(device, resolveSession, s => s.SetMuted(!s.Volume.Muted), d => d.VolumeMute());
            }

            return RestPayload.Done("/togglemute" + action);
        }

        private static string Volume(string action, IDevices devices, Func<IDevice, IPlaybackSession> resolveSession)
        {
            var device = GetDevice(devices, action);
            if (device == null)
                return errorDeviceNotFound;

            var deviceVolume = action.Split('/')?.Length > 2 ? action.Split('/')[2] : null;
            if (string.IsNullOrEmpty(deviceVolume))
                return errorWrongVolume;

            if (!int.TryParse(deviceVolume, out int level))
                return errorWrongVolume;

            if (level < 0 || level > 100)
                return errorWrongVolume;

            Control(device, resolveSession, s => s.SetVolume(level / 100.0f), d => d.VolumeSet(level / 100.0f));

            return RestPayload.Done("/volume" + action);
        }

        private static string Stop(string action, IDevices devices, Func<IDevice, IPlaybackSession> resolveSession)
        {
            if (string.IsNullOrEmpty(action.Replace("/", "")))
            {
                foreach (var device in devices.GetDeviceList())
                    Control(device, resolveSession, s => s.Stop(), d => d.Stop(true));
            }
            else
            {
                var device = GetDevice(devices, action);
                if (device == null)
                    return errorDeviceNotFound;

                Control(device, resolveSession, s => s.Stop(), d => d.Stop(true));
            }

            return RestPayload.Done("/stop" + action);
        }

        private static string Start(string action, IDevices devices, Func<IDevice, IPlaybackSession> resolveSession)
        {
            if (string.IsNullOrEmpty(action.Replace("/", "")))
            {
                foreach (var device in devices.GetDeviceList())
                    Control(device, resolveSession, s => s.TogglePlayStop(), d => d.OnClickPlayStop());
            }
            else
            {
                var device = GetDevice(devices, action);
                if (device == null)
                    return errorDeviceNotFound;

                Control(device, resolveSession, s => s.TogglePlayStop(), d => d.OnClickPlayStop());
            }

            return RestPayload.Done("/start" + action);
        }

        // 2.2b-4.4f: route one device's control action through the neutral session, with fallbacks.
        //   resolveSession == null -> no provider composed: use the legacy direct device path.
        //   session == null        -> device left the registry: skip (no-op), matching the old
        //                             disposed-device guard; keeps broadcast fan-out best-effort.
        private static void Control(IDevice device, Func<IDevice, IPlaybackSession> resolveSession,
            Func<IPlaybackSession, Task> viaSession, Action<IDevice> viaDevice)
        {
            if (resolveSession == null) { viaDevice(device); return; }
            var session = resolveSession(device);
            if (session != null) _ = viaSession(session);   // 2.2b-M2: fire-and-forget (Chromecast completes synchronously)
        }

        private static IDevice? GetDevice(IDevices devices, string action)
        {
            if (action.IndexOf("/") != 0)
                return null;

            var deviceName = action.Split('/')?.Length > 0 ? action.Split('/')[1] : null;
            if (string.IsNullOrEmpty(deviceName))
                return null;

            var deviceList = devices.GetDeviceList();
            foreach (var device in deviceList)
            {
                if (device.GetFriendlyName().ToLowerInvariant() == deviceName)
                    return device;
            }

            return null;
        }
    }
}
