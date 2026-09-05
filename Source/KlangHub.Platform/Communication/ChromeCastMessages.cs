using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Google.Protobuf;
using KlangHub.ProtocolBuffer;
using KlangHub.Communication.Classes;
using KlangHub.Core.NowPlaying;
using KlangHub.Communication.Interfaces;
using System.Text.Json;

namespace KlangHub.Communication
{
    /// <summary>
    /// Classes used to send messages to a Chromecast device.
    /// </summary>
    public class ChromeCastMessages : IChromeCastMessages
    {
        private const string namespaceConnect = "urn:x-cast:com.google.cast.tp.connection";
        private const string namespaceHeartbeat = "urn:x-cast:com.google.cast.tp.heartbeat";
        private const string namespaceReceiver = "urn:x-cast:com.google.cast.receiver";
        private const string namespaceMedia = "urn:x-cast:com.google.cast.media";

        public CastMessage GetVolumeSetMessage(Volume volume, int requestId, string? sourceId = null, string? destinationId = null)
        {
            if (volume == null)
                return null!;

            var volumeMessage = new MessageVolume
            {
                type = "SET_VOLUME",
                volume = new SendVolume
                {
                    level = volume.level
                },
                requestId = requestId
            };
            return GetCastMessage(volumeMessage, namespaceReceiver, sourceId, destinationId);
        }

        public CastMessage GetVolumeMuteMessage(bool muted, int requestId, string? sourceId = null, string? destinationId = null)
        {
            var volumeMessage = new MessageVolumeMute
            {
                type = "SET_VOLUME",
                volume = new SendVolumeMute
                {
                    muted = muted
                },
                requestId = requestId
            };
            return GetCastMessage(volumeMessage, namespaceReceiver, sourceId, destinationId);
        }

        public CastMessage GetConnectMessage(string? sourceId = null, string? destinationId = null)
        {
            return GetCastMessage(new PayloadMessageBase { type = "CONNECT" }, namespaceConnect, sourceId, destinationId);
        }

        public CastMessage GetCloseMessage()
        {
            return GetCastMessage(new PayloadMessageBase { type = "CLOSE" }, namespaceConnect, null, null);
        }

        /// <summary>Google's Default Media Receiver - the app every Cast device already has.</summary>
        public const string DefaultReceiverAppId = "CC1AD845";

        /// <summary>
        /// The receiver application to launch. Empty means Google's default; an eight-character id from the
        /// Cast Developer Console launches KlangHub's own receiver instead, which is what puts our name and
        /// our colours on the television (see receiver/README.md).
        ///
        /// Read at LAUNCH time, not when this object was made. It used to be copied in at construction,
        /// which happens when a device is discovered - so a pasted id reached only speakers found after the
        /// paste. The others kept launching Google's default while the settings field and the diagnostics
        /// header both claimed otherwise, and a house ended up running two different receivers at once.
        /// </summary>
        public string ReceiverAppId
        {
            get => receiverAppId ?? CastReceiver.AppId;
            set => receiverAppId = value;
        }

        private string? receiverAppId;

        public CastMessage GetLaunchMessage(int requestId)
        {
            var appId = string.IsNullOrWhiteSpace(ReceiverAppId) ? DefaultReceiverAppId : ReceiverAppId.Trim();
            var message = new MessageLaunch { type = "LAUNCH", appId = appId, requestId = requestId };
            return GetCastMessage(message, namespaceReceiver);
        }

        public CastMessage GetLoadMessage(string streamingUrl, string sourceId, string destinationId, int requestId, CastMediaMetadata meta)
        {
            var images = new List<Image>();
            if (!string.IsNullOrWhiteSpace(meta.ImageUrl))
                images.Add(new Image { url = meta.ImageUrl, width = 1280, height = 1280 });

            var message = new MessageLoad
            {
                type = "LOAD",
                autoplay = true,
                currentTime = 0,
                activeTrackIds = new List<object>(),
                repeatMode = "REPEAT_OFF",
                media = new Media
                {
                    contentId = streamingUrl,
                    contentType = meta.ContentType,
                    // LIVE, not BUFFERED: this is an endless capture of what the PC is playing right now.
                    // It has no duration, no end and nothing to seek to. Declaring it BUFFERED tells the
                    // receiver it is looking at a file - so it shows a progress bar that can never fill and
                    // may try to fetch ranges of something that does not exist. LIVE is what it actually is.
                    streamType = "LIVE",
                    metadata = new Metadata
                    {
                        // metadataType 3 = MusicTrackMediaMetadata: the Default Media Receiver renders the
                        // artwork full-screen and overlays title/artist/album — a premium screen, not the
                        // generic "Default Media Receiver" placeholder.
                        type = 0,
                        metadataType = 3,
                        title = meta.Title,
                        artist = meta.Subtitle,
                        albumName = meta.Album,
                        images = images
                    },
                },
                requestId = requestId
            };
            return GetCastMessage(message, namespaceMedia, sourceId, destinationId);
        }

        public CastMessage GetPauseMessage(string sessionId, int mediaSessionId, int requestId, string sourceId, string destinationId)
        {
            return GetCastMessage(new MessagePause { type = "PAUSE", sessionId = sessionId, mediaSessionId = mediaSessionId, requestId = requestId }, namespaceMedia, sourceId, destinationId);
        }

        public CastMessage GetPlayMessage(string sessionId, int mediaSessionId, int requestId, string sourceId, string destinationId)
        {
            return GetCastMessage(new MessagePause { type = "PLAY", sessionId = sessionId, mediaSessionId = mediaSessionId, requestId = requestId }, namespaceMedia, sourceId, destinationId);
        }

        public CastMessage GetPingMessage()
        {
            return GetCastMessage(new PayloadMessageBase { type = "PING" }, namespaceHeartbeat);
        }

        public CastMessage GetPongMessage()
        {
            return GetCastMessage(new PayloadMessageBase { type = "PONG" }, namespaceHeartbeat);
        }

        public CastMessage GetReceiverStatusMessage(int requestId)
        {
            return GetCastMessage(new MessageStatus { type = "GET_STATUS", requestId = requestId }, namespaceReceiver);
        }

        public CastMessage GetMediaStatusMessage(int requestId, string sourceId, string destinationId)
        {
            return GetCastMessage(new MessageStatus { type = "GET_STATUS", requestId = requestId }, namespaceMedia, sourceId, destinationId);
        }

        public CastMessage GetStopMessage(string sessionId, int mediaSessionId, int requestId, string sourceId, string destinationId)
        {
            return GetCastMessage(new MessagePause { type = "STOP", sessionId = sessionId, mediaSessionId = mediaSessionId, requestId = requestId }, namespaceMedia, sourceId, destinationId);
        }

        /// <summary>
        /// Ends the receiver APPLICATION (sender-0 -> receiver-0), which is what sends a television back to
        /// whatever it was showing before - its menu, its input, its screensaver. The media STOP above only
        /// ends playback: the Default Media Receiver stays loaded and the Cast logo sits on the screen until
        /// the device times out on its own, which is minutes of a bright logo burnt into a dark living room.
        /// </summary>
        public CastMessage GetQuitApplicationMessage(string sessionId, int requestId)
        {
            var message = new MessageQuitApplication { type = "STOP", sessionId = sessionId, requestId = requestId };
            return GetCastMessage(message, namespaceReceiver);
        }

        /// <summary>
        /// KlangHub's own channel to KlangHub's own receiver. Must match the namespace the receiver
        /// registers character for character: a mismatch fails silently on both ends - the device simply
        /// never delivers the message, with nothing logged anywhere to say so.
        /// </summary>
        public const string namespaceStage = "urn:x-cast:de.klanghub.stage";

        /// <summary>
        /// Null fields are dropped rather than written as null, because on the receiving side "absent"
        /// means "no news, keep what you have" while a null would read as "clear this line".
        /// </summary>
        private static readonly JsonSerializerOptions stageJson = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// A new piece, or a correction to the one playing. This is what makes a track change possible
        /// without a fresh LOAD - and a LOAD on a loopback stream is one to three seconds of silence.
        /// </summary>
        public CastMessage GetStageTrackMessage(StageUpdate update, string sourceId, string destinationId)
        {
            var message = new MessageStageTrack
            {
                type = "track",
                title = update.Title,
                artist = update.Artist,
                album = update.Album,
                zone = update.Zone,
                cover = update.Cover,
                quality = update.Quality,
                duration = update.Duration,
                newTrack = update.NewTrack
            };

            return GetStageMessage(message, sourceId, destinationId);
        }

        public CastMessage GetStagePositionMessage(double position, double? duration, string sourceId, string destinationId)
        {
            var message = new MessageStagePosition { type = "position", position = position, duration = duration };
            return GetStageMessage(message, sourceId, destinationId);
        }

        public CastMessage GetStageStateMessage(bool playing, string sourceId, string destinationId)
        {
            var message = new MessageStageState { type = "state", playing = playing };
            return GetStageMessage(message, sourceId, destinationId);
        }

        /// <summary>
        /// Addressed to the receiver application, never to sender-0/receiver-0. That pair is the device's
        /// own platform channel; our receiver lives at the transport id the RECEIVER_STATUS handed back,
        /// and a stage message sent to the platform is simply dropped.
        /// </summary>
        private CastMessage GetStageMessage(PayloadMessageBase message, string sourceId, string destinationId)
        {
            var json = JsonSerializer.Serialize(message, message.GetType(), stageJson);
            return new CastMessage
            {
                ProtocolVersion = CastMessage.Types.ProtocolVersion.Castv210,
                SourceId = sourceId,
                DestinationId = destinationId,
                PayloadType = CastMessage.Types.PayloadType.String,
                Namespace = namespaceStage,
                PayloadUtf8 = json
            };
        }

        public CastMessage GetCastMessage(PayloadMessageBase message, string msgNamespace, string? sourceId = null, string? destinationId = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) sourceId = "sender-0";
            if (string.IsNullOrWhiteSpace(destinationId)) destinationId = "receiver-0";

            string jsonMessage = JsonSerializer.Serialize(message, message.GetType());
            // No builder in the modern runtime - the message itself is mutable until it is sent. The two
            // enum fields are assigned explicitly rather than left at their default, because the .proto
            // declares every field `optional`: what is assigned is what goes on the wire, and a Cast
            // receiver requires both of them to be there.
            return new CastMessage
            {
                ProtocolVersion = CastMessage.Types.ProtocolVersion.Castv210,
                SourceId = sourceId,
                DestinationId = destinationId,
                PayloadType = CastMessage.Types.PayloadType.String,
                Namespace = msgNamespace,
                PayloadUtf8 = jsonMessage
            };
        }

        public byte[] MessageToByteArray(CastMessage message)
        {
            if (message == null)
                return new byte[0];

            var bufMsg = message.ToByteArray();

            var bufLen = new byte[4];
            bufLen = BitConverter.GetBytes(bufMsg.Length);
            bufLen = bufLen.Reverse().ToArray();

            return bufLen.Concat(bufMsg).ToArray();
        }
    }
}
