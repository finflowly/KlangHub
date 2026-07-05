using System;
using System.Threading;

namespace KlangHub.Core.Casting
{
    /// <summary>
    /// The host side of a cast session: supplies the stream endpoint/title, the auto-restart policy,
    /// and a task runner. Implemented by the app's ApplicationLogic (via IApplicationLogic : ICastHost)
    /// and consumed by the Chromecast Device/DeviceCommunication, so the Platform layer needs no
    /// reference to the App's IApplicationLogic.
    /// </summary>
    public interface ICastHost
    {
        string GetStreamingUrl();
        string GetStreamTitle();
        /// <summary>The MIME type for the currently selected stream format (codec-aware; see StreamCodec).</summary>
        string GetStreamContentType();
        bool GetAutoRestart();
        void StartTask(Action action, CancellationTokenSource? cancellationTokenSource = null);
    }
}
