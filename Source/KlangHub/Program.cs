using System;
using System.Linq;
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
        static void Main(string[] args)
        {
            // "--lang=xx": the installer starts the app once in the language chosen during setup.
            var lang = args?.FirstOrDefault(a => a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))?[7..];
            if (!string.IsNullOrWhiteSpace(lang) && MainForm.SupportedCultures.Contains(lang!.ToLowerInvariant()))
                Classes.StartupOptions.Culture = lang!.ToLowerInvariant();

            // Per-Monitor-V2 DPI (available on .NET 10) keeps the owner-drawn cards, meters and faders crisp
            // across mixed-DPI monitors. Must be the first UI call in Main.
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            // One KlangHub per signed-in user. A second copy would fight the first for the REST port, the
            // streaming port and the audio device; since the close button began hiding the window in the
            // notification area, clicking the desktop icon again is the natural way to start one. Ask the
            // running copy to show itself and leave.
            using var claim = Classes.SingleInstance.TryAcquire();
            if (!claim.IsOnlyInstance)
            {
                Classes.SingleInstance.AskTheRunningCopyToShowItself();
                return;
            }

            try
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
            catch (Exception ex)
            {
                // This used to be an empty catch around the whole of startup. On a machine where KlangHub
                // failed to start - and a first release will find such machines - the user double-clicked
                // the icon and nothing whatever happened: no window, no message, no log line, nothing to
                // send anybody. It was the worst possible first impression, and it cost us the only bug
                // report that would have explained it.
                ReportAndRecord("KlangHub could not start.", ex);
            }
        }

        private static void UnhandledHandler(object sender, System.UnhandledExceptionEventArgs e)
        {
            // Not behind #if DEBUG. A release build is exactly where this matters: it is the last thing
            // that runs before the process disappears, and without it the disappearance has no witness.
            ReportAndRecord("KlangHub has to close.", e.ExceptionObject as Exception);
        }

        /// <summary>
        /// Write the failure somewhere it can be found again, then say so once, plainly.
        /// <para>
        /// The file comes first and on its own: the message box needs a message pump, and by the time
        /// this runs there may not be one left.
        /// </para>
        /// </summary>
        private static void ReportAndRecord(string headline, Exception? ex)
        {
            var detail = ex?.ToString() ?? "No further detail.";

            var crashFile = string.Empty;
            try
            {
                var folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KlangHub");
                System.IO.Directory.CreateDirectory(folder);
                crashFile = System.IO.Path.Combine(folder, "startup-error.txt");
                System.IO.File.WriteAllText(crashFile,
                    $"{DateTimeOffset.Now:u}{Environment.NewLine}{headline}{Environment.NewLine}{detail}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Nowhere to write it. The message below is then all there is, which is still more than
                // the silence this replaces.
            }

            try
            {
                var where = string.IsNullOrEmpty(crashFile)
                    ? string.Empty
                    : $"{Environment.NewLine}{Environment.NewLine}Written to:{Environment.NewLine}{crashFile}";

                MessageBox.Show($"{headline}{Environment.NewLine}{Environment.NewLine}{ex?.Message}{where}",
                    "KlangHub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // No message pump left to show it on. The file is written; that was the important half.
            }
        }
    }
}
