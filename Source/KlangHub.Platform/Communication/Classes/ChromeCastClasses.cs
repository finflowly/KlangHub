using System.Collections.Generic;

namespace KlangHub.Communication.Classes
{
    /// <summary>
    /// Classes used for messages received from a Chromecast device.
    /// </summary>
    public class PayloadMessageBase
    {
        public string type { get; set; } = null!;
    }

    public class MessageVolume : PayloadMessageBase
    {
        public SendVolume volume { get; set; } = null!;
        public int requestId { get; set; }
    }

    public class SendVolume
    {
        public float level { get; set; }
    }

    public class MessageVolumeMute : PayloadMessageBase
    {
        public SendVolumeMute volume { get; set; } = null!;
        public int requestId { get; set; }
    }

    public class SendVolumeMute
    {
        public bool muted { get; set; }
    }

    public class MessageLaunch : PayloadMessageBase
    {
        public string appId { get; set; } = null!;
        public int requestId { get; set; }
    }

    public class MessageLoad : PayloadMessageBase
    {
        public bool autoplay { get; set; }
        public float currentTime { get; set; }
        public List<object> activeTrackIds { get; set; } = null!;
        public string repeatMode { get; set; } = null!;
        public Media media { get; set; } = null!;
        public int requestId { get; set; }
    }

    public class MessagePause : PayloadMessageBase
    {
        public int mediaSessionId { get; set; }
        public string sessionId { get; set; } = null!;
        public int requestId { get; set; }
    }

    /// <summary>
    /// STOP on the RECEIVER namespace: ends the receiver application itself, as opposed to the media STOP
    /// which only ends playback. It carries the application session id and no media session.
    /// </summary>
    public class MessageQuitApplication : PayloadMessageBase
    {
        public string sessionId { get; set; } = null!;
        public int requestId { get; set; }
    }

    public class MessageStatus : PayloadMessageBase
    {
        public int requestId { get; set; }
    }

    public class Media
    {
        public string contentId { get; set; } = null!;
        public string contentType { get; set; } = null!;
        public string streamType { get; set; } = null!;
        public Metadata metadata { get; set; } = null!;
    }

    public class Metadata
    {
        public int type { get; set; }
        public int metadataType { get; set; }
        public string title { get; set; } = null!;
        public string? artist { get; set; }
        public string? albumName { get; set; }
        public List<Image> images { get; set; } = null!;
    }

    public class Image
    {
        public string url { get; set; } = null!;
        public int width { get; set; }
        public int height { get; set; }
    }

    /// <summary>
    /// If the stream couldn't be opened (socket problems etc.) you get this message from the device.
    /// type = 'LOAD_FAILED'
    /// </summary>
    public class MessageLoadFailed : PayloadMessageBase
    {
        public int requestId { get; set; }
    }

    /// <summary>
    /// If calling 'LOAD' a second time with the same url and the first is still 'loading'
    /// , you get this message from the device.
    /// type = 'LOAD_CANCELLED'
    /// </summary>
    public class MessageLoadCancelled : PayloadMessageBase
    {
        public int requestId { get; set; }
    }

    /// <summary>
    /// You get this message after LOAD, Volume change, Mute etc., and when you request the status.
    /// type = 'MEDIA_STATUS'
    /// </summary>
    public class MessageMediaStatus : PayloadMessageBase
    {
        public List<MediaStatus> status { get; set; } = null!;
        public int requestId { get; set; }
    }

    public class MediaStatus
    {
        public int mediaSessionId { get; set; }
        public int playbackRate { get; set; }
        public string playerState { get; set; } = null!;
        public float currentTime { get; set; }
        public int supportedMediaCommands { get; set; }
        public Volume volume { get; set; } = null!;
        public List<object> activeTrackIds { get; set; } = null!;
        public Media media { get; set; } = null!;
        public int currentItemId { get; set; }
        public ExtendedStatus extendedStatus { get; set; } = null!;
        public string repeatMode { get; set; } = null!;
    }

    public class ExtendedStatus
    {
        public string playerState { get; set; } = null!;
        public Media media { get; set; } = null!;
    }

    /// <summary>
    /// What KlangHub tells its own receiver over its own namespace.
    /// <para>
    /// Every field is optional and null ones are left off the wire entirely (see the serializer options in
    /// ChromeCastMessages). That is not tidiness: the stage merges what it is given and keeps the rest, so
    /// a key arriving as null would wipe a line the television is currently showing correctly - which is
    /// exactly the flickering the metadata cascade exists to prevent.
    /// </para>
    /// </summary>
    public class MessageStageTrack : PayloadMessageBase
    {
        public string? title { get; set; }
        public string? artist { get; set; }
        public string? album { get; set; }
        public string? zone { get; set; }
        public string? cover { get; set; }
        public string? quality { get; set; }
        public double? duration { get; set; }
        public bool newTrack { get; set; }
    }

    /// <summary>Where we are in the piece. Small on purpose - it goes out every few seconds.</summary>
    public class MessageStagePosition : PayloadMessageBase
    {
        public double position { get; set; }
        public double? duration { get; set; }
    }

    /// <summary>Playing or held. The stage dims rather than clears when the music is paused.</summary>
    public class MessageStageState : PayloadMessageBase
    {
        public bool playing { get; set; }
    }

    /// <summary>
    /// A device that holds a LAUNCH back until somebody allows it at the device answers with this rather
    /// than with a RECEIVER_STATUS. Note <c>launchRequestId</c>: it is deliberately not <c>requestId</c>,
    /// so matching this message by request number - the way every other answer is matched - never works.
    /// Observed on a Harman Kardon Enchant on 2026-09-05:
    /// <c>{"launchRequestId":25,"status":"USER_ALLOWED","type":"LAUNCH_STATUS"}</c>.
    /// </summary>
    public class MessageLaunchStatus : PayloadMessageBase
    {
        public int launchRequestId { get; set; }
        public string? status { get; set; }
    }

    /// <summary>
    /// After a 'LAUNCH' you get this message from the device, or when you request the device for it.
    /// type = 'RECEIVER_STATUS'
    /// </summary>
    public class MessageReceiverStatus : PayloadMessageBase
    {
        public int requestId { get; set; }
        public ReceiverStatus status { get; set; } = null!;
    }

    public class ReceiverStatus
    {
        public List<Application> applications { get; set; } = null!;
        public Volume volume { get; set; } = null!;
    }

    public class Volume
    {
        public string controlType { get; set; } = null!;
        public float level { get; set; }
        public bool muted { get; set; }
        public float stepInterval { get; set; }
    }

    public class Application
    {
        public string appId { get; set; } = null!;
        public string displayName { get; set; } = null!;
        public bool isIdleScreen { get; set; }
        public List<Namespaces> namespaces { get; set; } = null!;
        public string sessionId { get; set; } = null!;
        public string statusText { get; set; } = null!;
        public string transportId { get; set; } = null!;
    }

    public class Namespaces
    {
        public string name { get; set; } = null!;
    }
}
