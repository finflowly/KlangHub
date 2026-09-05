using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KlangHub.Core.NowPlaying;

namespace KlangHub.Platform.Clementine
{
    /// <summary>
    /// Listens to Clementine over its network remote.
    /// <para>
    /// Why this is worth a TCP client and a protocol: measured on 2026-09-05, Clementine playing a track
    /// reports <b>nothing</b> to Windows' now-playing session. Its window title gives an artist and a
    /// title and nothing else. This connection gives the album, the length, the position, the file on
    /// disc and the cover art as bytes - the difference between a name on the television and a record
    /// sleeve filling it.
    /// </para>
    /// <para>
    /// No configuration: it tries the standard port on this machine and stays quiet when nothing answers.
    /// A listener who has switched the remote on in Clementine gets a full screen; one who has not loses
    /// nothing, because every other source still runs.
    /// </para>
    /// </summary>
    public sealed class ClementineRemoteSource : INowPlayingSource
    {
        /// <summary>Clementine's own default. Changing it there is rare enough to be a setting later.</summary>
        public const int Port = 5500;

        /// <summary>
        /// Where to look for Clementine, in order - and the order is load-bearing.
        /// <para>
        /// Clementine refuses connections it does not consider to come from a private network. Its check
        /// (NetworkRemote::IpIsPrivate) lists 127.0.0.0/8 and ::1/128, but <b>not</b> ::ffff:127.0.0.1 -
        /// which is precisely what IPv4 localhost looks like when it arrives on the IPv6 dual-stack
        /// socket. On Windows that is the socket doing the listening, so connecting to 127.0.0.1 gets a
        /// TCP handshake, a verdict of "connection from public ip", and a silent close: no error, no
        /// disconnect message, nothing in any log. Measured 2026-09-05; ::1 works immediately.
        /// </para>
        /// IPv4 stays in the list for a machine with IPv6 switched off.
        /// </summary>
        public static readonly string[] Hosts = { "::1", "127.0.0.1" };

        /// <summary>The protocol version Clementine's own clients announce.</summary>
        private const int ProtocolVersion = 21;

        /// <summary>A frame larger than this is not something we sent for - a cover is a few hundred KB.</summary>
        private const int LargestSensibleFrame = 16 * 1024 * 1024;

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

        private readonly Action<string> log;
        private readonly CancellationTokenSource life = new();
        private bool disposed;
        private bool everConnected;

        public ClementineRemoteSource(Action<string> logIn)
        {
            log = logIn;
        }

        public MetadataSource Source => MetadataSource.PlayerRemote;

        public event EventHandler<NowPlayingTrack>? Reported;

        /// <summary>Raised when the player starts or holds. Lets the stage dim rather than clear.</summary>
        public event EventHandler<bool>? PlayingChanged;

        public void Start()
        {
            _ = Task.Run(() => RunAsync(life.Token));
        }

        /// <summary>
        /// Connects, listens, and keeps coming back. Clementine may not be running yet, may be closed
        /// mid-evening and started again - none of which is an error worth telling anybody about.
        /// </summary>
        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ListenOnce(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Only ever mentioned once per session: a player that is simply not running would
                    // otherwise write a line every ten seconds all evening.
                    if (everConnected)
                    {
                        log($"now-playing: lost the connection to Clementine ({ex.Message}) - will try again");
                        everConnected = false;
                    }
                }

                try
                {
                    await Task.Delay(RetryDelay, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task ListenOnce(CancellationToken token)
        {
            using var client = await Connect(token);
            if (client == null)
                return;

            everConnected = true;
            log("now-playing: connected to Clementine's network remote");

            using var stream = client.GetStream();

            // The handshake. send_playlist_songs stays false on purpose: KlangHub wants to know what is
            // playing, not to receive an entire library over a socket.
            await Send(stream, new Message
            {
                Version = ProtocolVersion,
                Type = MsgType.Connect,
                RequestConnect = new RequestConnect { AuthCode = 0, SendPlaylistSongs = false, Downloader = false }
            }, token);

            while (!token.IsCancellationRequested)
            {
                var message = await Receive(stream, token);
                if (message == null)
                    return;

                Handle(message);
            }
        }

        /// <summary>
        /// Reaches Clementine on the first address that answers. Both are tried every time rather than
        /// remembered: a machine's IPv6 stack can come and go between one evening and the next.
        /// </summary>
        private async Task<TcpClient?> Connect(CancellationToken token)
        {
            foreach (var host in Hosts)
            {
                var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(host, Port, token);
                    return client;
                }
                catch (OperationCanceledException)
                {
                    client.Dispose();
                    throw;
                }
                catch (Exception)
                {
                    // Nothing there on this address. Not worth a word - Clementine simply may not be running.
                    client.Dispose();
                }
            }

            return null;
        }

        private void Handle(Message message)
        {
            switch (message.Type)
            {
                case MsgType.CurrentMetainfo:
                    var track = ClementineSong.ToTrack(message.ResponseCurrentMetadata?.SongMetadata);
                    if (!track.IsEmpty)
                        Reported?.Invoke(this, track);
                    break;

                case MsgType.EngineStateChanged:
                    var state = message.ResponseEngineStateChanged?.State ?? EngineState.Empty;
                    PlayingChanged?.Invoke(this, state == EngineState.Playing);
                    break;

                // KEEP_ALIVE needs no answer; receiving it is the answer. UPDATE_TRACK_POSITION is read
                // by the stage's own clock rather than pushed on every tick - see StageUpdate.
                default:
                    break;
            }
        }

        /// <summary>
        /// Four bytes of length, big-endian, then the message - the same framing the Cast protocol uses,
        /// which is why the reader below looks familiar.
        /// </summary>
        private static async Task Send(NetworkStream stream, Message message, CancellationToken token)
        {
            var body = message.ToByteArray();
            var frame = new byte[4 + body.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);
            Buffer.BlockCopy(body, 0, frame, 4, body.Length);

            await stream.WriteAsync(frame, token);
            await stream.FlushAsync(token);
        }

        private static async Task<Message?> Receive(NetworkStream stream, CancellationToken token)
        {
            var header = new byte[4];
            if (!await ReadExactly(stream, header, token))
                return null;

            var length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (length <= 0 || length > LargestSensibleFrame)
                return null;   // out of step with the stream; a fresh connection is the only cure

            var body = new byte[length];
            if (!await ReadExactly(stream, body, token))
                return null;

            try
            {
                return Message.Parser.ParseFrom(body);
            }
            catch (InvalidProtocolBufferException)
            {
                // A message this build does not understand. Skipping it is right; giving up the connection
                // over it would not be.
                return new Message();
            }
        }

        private static async Task<bool> ReadExactly(NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(read), token);
                if (n == 0)
                    return false;   // the other end closed

                read += n;
            }

            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            try { life.Cancel(); } catch (Exception) { /* already going away */ }
            life.Dispose();
        }
    }
}
