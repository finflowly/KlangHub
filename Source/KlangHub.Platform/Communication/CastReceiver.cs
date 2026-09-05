using System;

namespace KlangHub.Communication
{
    /// <summary>
    /// Which Cast receiver application KlangHub launches.
    /// <para>
    /// Empty means Google's Default Media Receiver - the app every Cast device already has, and what the
    /// television labels "Default Media Receiver". An id registered in the Cast Developer Console launches
    /// KlangHub's own receiver instead: our name, our colours, and later the synchronisation that only a
    /// receiver we control can provide (see receiver/README.md and docs/PLAN-MULTIROOM-SYNC.md).
    /// </para>
    /// A single process-wide value rather than a constructor parameter: every device connection needs the
    /// same id, and it changes at most once, when the user pastes it into the settings.
    /// </summary>
    public static class CastReceiver
    {
        private static string appId = string.Empty;

        /// <summary>The registered application id, or an empty string for Google's default receiver.</summary>
        public static string AppId
        {
            get => appId;
            set => appId = (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Is the application a device reports in its RECEIVER_STATUS the one we asked it to launch?
        /// <para>
        /// The launch has honoured a pasted id for a while, but the answer used to be matched against a
        /// hard-coded default-receiver id. Entering KlangHub's own id therefore launched our receiver and
        /// then discarded the reply that carries the transport id and session - no media was ever loaded.
        /// </para>
        /// Case and stray spaces are forgiven: the id is copied out of the developer console by hand.
        /// </summary>
        public static bool Matches(string? configuredAppId, string? reportedAppId)
        {
            if (string.IsNullOrWhiteSpace(reportedAppId))
                return false;

            var wanted = string.IsNullOrWhiteSpace(configuredAppId)
                ? ChromeCastMessages.DefaultReceiverAppId
                : configuredAppId.Trim();

            return string.Equals(wanted, reportedAppId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Is this the application the currently configured id asks for?</summary>
        public static bool IsOurApplication(string? reportedAppId) => Matches(AppId, reportedAppId);
    }
}
