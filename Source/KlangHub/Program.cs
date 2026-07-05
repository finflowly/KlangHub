using System;
using System.Linq;
using Microsoft.VisualBasic.ApplicationServices;
using System.Windows.Forms;
using KlangHub.Application;
using KlangHub.Streaming;
using KlangHub.Platform.Audio;
using KlangHub.Discover;
using KlangHub.Platform.Casting.Chromecast;
using KlangHub.Platform.Casting.AirPlay;
using KlangHub.Platform.Casting.Snapcast;
using KlangHub.Platform.Casting.Shared;

namespace KlangHub
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Per-Monitor-V2 DPI (available on .NET 10) keeps the owner-drawn cards, meters and faders crisp
            // across mixed-DPI monitors. Must be the first UI call in Main.
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                //new SingleInstanceController().Run(new string[0]);
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(UnhandledHandler);

                var logger = new Logger();
                var devices = new Devices(logger);
                var discoverDevices = new DiscoverDevices(logger);
                var chromecastDiscovery = new ChromecastDeviceDiscovery(discoverDevices);
                chromecastDiscovery.DeviceDiscovered += (s, d) => { if (chromecastDiscovery.TryGetDevice(d.Id, out var full)) devices.OnDeviceAvailable(full); };
                var chromecastProvider = new ChromecastProvider(
                    chromecastDiscovery,
                    descriptor => devices.GetDeviceList()
                        .OfType<IPlaybackSession>()
                        .FirstOrDefault(s => s.Device.Id == descriptor.Id)!);
                // 2.2b-H4a-4 / M3-4: front the providers with a CompositeCastProvider so the app still sees
                // one ICastProvider. Chromecast keeps its direct wiring above; AirPlay + Snapcast join as
                // discovery-only providers (CreateSession throws until their sessions land in M4+).
                var castProvider = new CompositeCastProvider(new ICastProvider[]
                {
                    chromecastProvider,
                    new SnapcastProvider(new SnapcastDiscovery(new MdnsDiscovery(logger))),
                    new AirPlayProvider(new AirPlayDiscovery(new MdnsDiscovery(logger), logger)),
                });
                // 2.2b-M3-4: surface discovered non-Chromecast endpoints in the log (the tray device list is
                // still Chromecast-Device-based; a neutral-descriptor UI comes later).
                castProvider.Discovery.DeviceDiscovered += (s, d) =>
                {
                    if (d.Provider != ProviderId.Chromecast)
                        logger.Log($"Discovered {d.Provider} endpoint: {d.Name} ({d.Id})");
                };
                var mainForm = new MainForm(
                        new ApplicationLogic(devices
                            , discoverDevices
                            , new Configuration()
                            , new StreamingRequestsListener()
                            , new DeviceStatusTimer()
                            , logger, castProvider)
                        , devices
                        , new LoopbackCaptureEngine(logger)
                        , logger
                        , castProvider);
                System.Windows.Forms.Application.Run(mainForm);
            }
            catch (Exception)
            {
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static void UnhandledHandler(object sender, System.UnhandledExceptionEventArgs e)
        {
#if DEBUG
            Exception exception = (Exception)e.ExceptionObject;
            MessageBox.Show(exception.Message);
#endif
        }

        /// <summary>
        /// Make sure only one instance is running.
        /// </summary>
        public class SingleInstanceController : WindowsFormsApplicationBase
        {
            public SingleInstanceController()
            {
                IsSingleInstance = true;
                StartupNextInstance += MainFormStartupNextInstance;
            }

            void MainFormStartupNextInstance(object sender, StartupNextInstanceEventArgs e)
            {
                var form = MainForm as MainForm;
                if (!form!.IsDisposed)
                {
                    form.Show();
                    form.TopMost = true;
                    form.TopMost = false;
                }
            }

            protected override void OnCreateMainForm()
            {
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(UnhandledHandler);

                var logger = new Logger();
                var devices = new Devices(logger);
                var discoverDevices = new DiscoverDevices(logger);
                var chromecastDiscovery = new ChromecastDeviceDiscovery(discoverDevices);
                chromecastDiscovery.DeviceDiscovered += (s, d) => { if (chromecastDiscovery.TryGetDevice(d.Id, out var full)) devices.OnDeviceAvailable(full); };
                var chromecastProvider = new ChromecastProvider(
                    chromecastDiscovery,
                    descriptor => devices.GetDeviceList()
                        .OfType<IPlaybackSession>()
                        .FirstOrDefault(s => s.Device.Id == descriptor.Id)!);
                // 2.2b-H4a-4 / M3-4: front the providers with a CompositeCastProvider so the app still sees
                // one ICastProvider. Chromecast keeps its direct wiring above; AirPlay + Snapcast join as
                // discovery-only providers (CreateSession throws until their sessions land in M4+).
                var castProvider = new CompositeCastProvider(new ICastProvider[]
                {
                    chromecastProvider,
                    new SnapcastProvider(new SnapcastDiscovery(new MdnsDiscovery(logger))),
                    new AirPlayProvider(new AirPlayDiscovery(new MdnsDiscovery(logger), logger)),
                });
                // 2.2b-M3-4: surface discovered non-Chromecast endpoints in the log (the tray device list is
                // still Chromecast-Device-based; a neutral-descriptor UI comes later).
                castProvider.Discovery.DeviceDiscovered += (s, d) =>
                {
                    if (d.Provider != ProviderId.Chromecast)
                        logger.Log($"Discovered {d.Provider} endpoint: {d.Name} ({d.Id})");
                };
                MainForm = new MainForm(
                        new ApplicationLogic(devices
                            , discoverDevices
                            , new Configuration()
                            , new StreamingRequestsListener()
                            , new DeviceStatusTimer()
                            , logger, castProvider)
                        , devices
                        , new LoopbackCaptureEngine(logger)
                        , logger
                        , castProvider);
                System.Windows.Forms.Application.Run(MainForm);
            }
        }
    }
}
