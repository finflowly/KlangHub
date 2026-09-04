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
    }
}
