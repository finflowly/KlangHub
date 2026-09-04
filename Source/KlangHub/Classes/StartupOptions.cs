namespace KlangHub.Classes
{
    /// <summary>
    /// Options handed to the app on the command line. The installer uses <see cref="Culture"/> to launch
    /// KlangHub in the language the user picked during setup, which need not be the Windows UI language —
    /// without it, a German user installing in English would still land in a German app.
    /// It only seeds the first run; from then on the language lives in the user's settings.
    /// </summary>
    public static class StartupOptions
    {
        /// <summary>Two-letter culture requested with <c>--lang=xx</c>, or null.</summary>
        public static string? Culture { get; set; }

        /// <summary>
        /// The Cast receiver application id KlangHub launches. Empty or null means Google's Default Media
        /// Receiver. A registered id (Cast Developer Console) launches KlangHub's own receiver instead - the
        /// one that carries our name and our colours on the television. It lives in the settings rather than
        /// in the code because the id belongs to whoever registered it: anyone forking the project needs
        /// their own (see receiver/README.md).
        /// </summary>
        public static string? ReceiverAppId { get; set; }
    }
}
