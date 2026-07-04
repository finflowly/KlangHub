using System;

namespace KlangHub.Core.Diagnostics
{
    /// <summary>
    /// Neutral logging contract. Lives in Core so both the App and the (Chromecast/Windows) Platform
    /// layer can depend on it without either referencing the other. Implementations stay outside Core.
    /// </summary>
    public interface ILogger
    {
        void Log(string message);
        void Log(Exception ex, string? message = null);
        void SetCallback(Action<string> logCallbackIn);
    }
}