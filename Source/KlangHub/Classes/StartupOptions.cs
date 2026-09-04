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
    }
}
