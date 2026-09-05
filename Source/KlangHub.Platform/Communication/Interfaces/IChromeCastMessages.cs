using KlangHub.Communication.Classes;
using KlangHub.ProtocolBuffer;

namespace KlangHub.Communication.Interfaces
{
    public interface IChromeCastMessages
    {
        CastMessage GetConnectMessage(string? sourceId, string? destinationId);
        CastMessage GetCloseMessage();
        CastMessage GetLaunchMessage(int requestId);
        CastMessage GetLoadMessage(string streamUrl, string chromeCastSource, string chromeCastDestination, int requestId, CastMediaMetadata metadata);
        CastMessage GetStopMessage(string chromeCastSessionId, int chromeCastMediaSessionId, int requestId, string chromeCastSource, string chromeCastDestination);
        CastMessage GetQuitApplicationMessage(string sessionId, int requestId);

        /// <summary>The receiver application to launch; empty = Google's Default Media Receiver.</summary>
        string ReceiverAppId { get; set; }
        CastMessage GetPauseMessage(string chromeCastSessionId, int chromeCastMediaSessionId, int requestId, string chromeCastSource, string chromeCastDestination);
        CastMessage GetPlayMessage(string chromeCastApplicationSessionNr, int chromeCastMediaSessionId, int v, string chromeCastSource, string chromeCastDestination);
        CastMessage GetVolumeSetMessage(Volume volumeSetting, int requestId, string? sourceId = null, string? destinationId = null);
        CastMessage GetVolumeMuteMessage(bool muted, int requestId, string? sourceId = null, string? destinationId = null);
        CastMessage GetPongMessage();
        CastMessage GetReceiverStatusMessage(int requestId);
        CastMessage GetMediaStatusMessage(int requestId, string chromeCastSource, string chromeCastDestination);
        byte[] MessageToByteArray(CastMessage castMessage);

        /// <summary>A new piece, or a correction, on KlangHub's own namespace.</summary>
        CastMessage GetStageTrackMessage(KlangHub.Core.NowPlaying.StageUpdate update, string sourceId, string destinationId);

        CastMessage GetStagePositionMessage(double position, double? duration, string sourceId, string destinationId);

        CastMessage GetStageStateMessage(bool playing, string sourceId, string destinationId);
    }
}