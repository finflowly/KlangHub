using System;
using System.Text;
using System.Net.Sockets;
using NAudio.Wave;
using KlangHub.Streaming.Interfaces;
using KlangHub.Classes;
using KlangHub.Application;
using KlangHub.Communication;
using System.Diagnostics;
using System.Threading;

namespace KlangHub.Streaming
{
    public class StreamingConnection : IStreamingConnection
    {
        private Socket? Socket;
        private IDevice device = null!;
        private ILogger logger = null!;
        private readonly IAudioHeader audioHeader;
        private bool isAudioHeaderSent;
        private int reduceLagCounter = 0;
        /// <summary>
        /// The size of each send block. Anything handed over in one piece must fit, because BufferBlock.Add
        /// is all-or-nothing: a block that does not fit is dropped whole. The startup cushion is the one
        /// caller that can approach this size, so it clamps against this constant rather than a number of
        /// its own (see ApplicationBuffer.MaxStartupBytes).
        /// </summary>
        public const int StreamBufferBytes = 10_000_000;

        private readonly Thread streamThread;
        private long bytesSent;

        /// <summary>
        /// Records how well this connection is actually being fed, and says something only when it is
        /// not. Two speakers fell behind real time and dropped their sockets on 2026-09-05, audibly, and
        /// the log could not say whether KlangHub had stopped supplying audio or the network had stopped
        /// carrying it - because nothing measured how long a send took. Now it does.
        /// </summary>
        private KlangHub.Core.Streaming.StreamHealth? health;
        private DateTime healthWindowStarted = DateTime.Now;
        private readonly DateTime startedAt = DateTime.Now;
        private BufferBlock bufferCaptured, bufferSend;
        readonly object bufferSwapSync = new();

        public StreamingConnection()
        {
            audioHeader = new AudioHeader();
            isAudioHeaderSent = false;
            bufferCaptured = new BufferBlock() { Data = new byte[StreamBufferBytes] };
            bufferSend = new BufferBlock() { Data = new byte[StreamBufferBytes] };

            streamThread = new Thread(StreamThread)
            {
                Name = "Stream Thread",
                IsBackground = true
            };
            streamThread.Start(new WeakReference<StreamingConnection>(this));
        }

        /// <summary>
        /// Thread for streaming the captured data.
        /// </summary>
        /// <param name="param">the streaming connection</param>
        private static void StreamThread(object? param)
        {
            var thisRef = (WeakReference<StreamingConnection>)param!;
            try
            {
                while (true)
                {
                    if (!thisRef.TryGetTarget(out StreamingConnection? streamer) || streamer == null)
                    {
                        // Instance is dead
                        return;
                    }

                    // Stream the data that is captured.
                    streamer.SwapBuffer();
                    // Tier2-E: send straight from the send buffer (zero allocation) instead of the old
                    // Take().ToArray() -> List.AddRange -> ToArray() triple copy. count is captured and Used
                    // reset before the send, preserving the original "reset-before-send" drop semantics; the
                    // buffer is stable until the next SwapBuffer and Socket.Send is synchronous.
                    streamer.ReportDroppedAudio();

                    var count = streamer.bufferSend.Used;
                    if (count > 0)
                    {
                        streamer.bufferSend.Used = 0;

                        try
                        {
                            // Timed on purpose: Socket.Send is synchronous, so a far end that has stopped
                            // taking data shows up here as a send that takes hundreds of milliseconds
                            // instead of single digits. That one number separates "the network stalled"
                            // from "we produced nothing".
                            var sendStarted = Stopwatch.GetTimestamp();
                            streamer.Socket?.Send(streamer.bufferSend.Data, 0, count, SocketFlags.None);
                            streamer.bytesSent += count;
                            streamer.RecordSend(count, Stopwatch.GetElapsedTime(sendStarted));
                        }
                        catch (Exception ex)
                        {
                            var deviceState = streamer.device?.GetDeviceState();
                            if (deviceState == DeviceState.Playing ||
                                deviceState == DeviceState.Buffering ||
                                deviceState == DeviceState.Paused)
                            {
                                streamer.Dispose();
                                streamer.logger.Log(ex, $"[{streamer.device!.GetHost()}:{streamer.device.GetPort()}] Disconnected Send after {streamer.Carried()}");
                                streamer.device?.SetDeviceState(DeviceState.ConnectError);
                                streamer.device?.CloseConnection();

                                // Rebuild the session NOW instead of leaving it to the 15 s status poll.
                                // Measured on 2026-09-05: a soundbar dropped the audio socket at 08:09:16
                                // and only came back at 08:09:59 - 37 of those 43 seconds were spent
                                // waiting, because setting the state and closing the connection asks
                                // nobody to do anything. The rebuild itself takes about six seconds.
                                // ResumeAfterConnectionLoss does nothing unless the user wants playback,
                                // and its gate makes sure the poll landing on top of this does not start
                                // a second LAUNCH.
                                streamer.device?.ResumeAfterConnectionLoss();
                            }
                        }
                    }

                    streamer.ReportHealth();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());

                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        /// <summary>How much audio a second of music actually is, so a shortfall can be recognised.</summary>
        private void RecordSend(int count, TimeSpan blocked)
        {
            health ??= new KlangHub.Core.Streaming.StreamHealth(expectedBytesPerSecond);
            health.Sent(count, blocked);
        }

        /// <summary>
        /// Says something once a second, and only when this connection is not keeping up. A line per
        /// second for a healthy stream would bury the one second that matters under the other nine
        /// hundred that do not.
        /// </summary>
        private void ReportHealth()
        {
            if (health == null)
                return;

            var window = DateTime.Now - healthWindowStarted;
            if (window < TimeSpan.FromSeconds(1))
                return;

            healthWindowStarted = DateTime.Now;
            var line = health.Report(window);
            if (line != null && device != null)
                logger?.Log($"[{device.GetHost()}:{device.GetPort()}] {line}");
        }

        /// <summary>
        /// What one second of this stream weighs on the wire, or <c>0</c> when the codec makes that
        /// unknowable. Worked out by <see cref="StreamCodec.WireBytesPerSecond"/> from the real capture
        /// format the sender hands over with the audio, never from an assumption: WAV at 48 kHz/24-bit and
        /// a 128 kbit MP3 differ by more than a factor of fifteen, and FLAC has no fixed answer at all.
        /// </summary>
        private volatile int expectedBytesPerSecond;

        private DateTime lastDropReport = DateTime.MinValue;
        private long pendingDroppedBytes;

        /// <summary>
        /// Says out loud when audio had to be thrown away, at most once every five seconds.
        ///
        /// A full buffer means the socket could not keep up. For WAV that is a click; for FLAC and MP3 it
        /// breaks the bitstream and the receiver stops with a decode error. Either way it must appear in the
        /// log, because a fault nobody can see is a fault nobody can fix.
        /// </summary>
        private void ReportDroppedAudio()
        {
            // Collected every time, reported at most every five seconds - and the running total is only
            // cleared when a line is actually written. Draining it on the suppressed path threw away
            // everything lost inside the quiet window, which on a real overrun is nearly all of it: this
            // loop runs about a thousand times a second, so the log would have shown one millisecond's
            // worth of loss and called it the whole story.
            lock (bufferSwapSync)
                pendingDroppedBytes += bufferCaptured.TakeDroppedBytes() + bufferSend.TakeDroppedBytes();

            if (pendingDroppedBytes <= 0 || logger == null)
                return;

            var now = DateTime.Now;
            if ((now - lastDropReport).TotalSeconds < 5)
                return;

            var dropped = pendingDroppedBytes;
            pendingDroppedBytes = 0;
            lastDropReport = now;
            logger.Log($"Disconnected-risk: dropped {dropped} bytes of audio - the send buffer was full. " +
                       "With FLAC or MP3 this breaks the stream for the receiver.");
        }

        /// <summary>
        /// Swap the captured and send buffers.
        /// </summary>
        private void SwapBuffer()
        {
            lock (bufferSwapSync)
            {
                var tmp = bufferCaptured;
                bufferCaptured = bufferSend;
                bufferSend = tmp;
            }
        }

        /// <summary>
        /// Send audio data to the device.
        /// </summary>
        /// <param name="dataToSend">the audio data</param>
        /// <param name="format">the audio format</param>
        /// <param name="reduceLagThreshold">lag control value</param>
        /// <param name="streamFormat">the stream format selected</param>
        public void SendData(byte[] dataToSend, AudioFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat)
        {
            if (dataToSend == null || dataToSend.Length == 0 || format == null)
                return;

            // Lag control: drop every n-th block to let a lagging device catch up.
            //
            // Only ever for uncompressed audio. Throwing a block out of WAV is a click; throwing one out of
            // FLAC or MP3 breaks the bitstream, and the receiver answers with a decode error and stops
            // (detailedErrorCode 102). The slider offered that to every user of a compressed format,
            // silently. Lag on FLAC or MP3 is a job for the buffer, not for the bin.
            if (reduceLagThreshold < 1000 && StreamCodec.IsWav(streamFormat))
            {
                reduceLagCounter++;
                if (reduceLagCounter > reduceLagThreshold)
                {
                    reduceLagCounter = 0;
                    return;
                }
            }

            // Remember what a second of this stream weighs, so the health watch can tell a shortfall from a
            // busy moment - and can tell that it must not try, which is the answer for FLAC.
            // No null test here: the guard at the top of the method already returned for a null format.
            // Testing again made the compiler treat everything after it as possibly-null again, which is
            // what CS8604 was reporting three lines further down - a warning about a check, not a bug.
            expectedBytesPerSecond = StreamCodec.WireBytesPerSecond(streamFormat, format.SampleRate, format.Channels, format.BitsPerSample);

            // Send the audio header before the first data - which for MP3 and FLAC means sending nothing,
            // because those streams already carry their own (see AudioHeader.GetStreamHeader).
            if (!isAudioHeaderSent)
            {
                isAudioHeaderSent = true;
                var header = audioHeader.GetStreamHeader(format, streamFormat);
                if (header.Length > 0)
                    Send(header);
            }

            Send(dataToSend);
        }

        /// <summary>
        /// Add the data to the buffer. 
        /// The actual sending is done in the thread.
        /// </summary>
        public void Send(byte[] data)
        {
            if (!IsConnected() || device == null || logger == null)
                return;

            lock (bufferSwapSync)
            {
                var currentBuffer = bufferCaptured;
                currentBuffer.Add(data, data.Length);
            }
        }

        /// <summary>
        /// Send the HTTP header. The Content-Type is codec-aware (see <see cref="StreamCodec"/>) so it always
        /// matches the LOAD payload and the bytes actually streamed — the old code hardcoded audio/wav even for
        /// MP3/FLAC streams.
        /// </summary>
        public void SendStartStreamingResponse(SupportedStreamFormat streamFormat)
        {
            var startStreamingResponse = Encoding.ASCII.GetBytes(GetStartStreamingResponse(streamFormat));
            Send(startStreamingResponse);
        }

        /// <summary>
        /// Return the HTTP header.
        /// </summary>
        private static string GetStartStreamingResponse(SupportedStreamFormat streamFormat)
        {
            var contentType = StreamCodec.ContentType(streamFormat);
            var fileName = StreamCodec.IsMp3(streamFormat) ? "stream.mp3"
                : StreamCodec.IsFlac(streamFormat) ? "stream.flac"
                : "stream.wav";

            var httpStartStreamingReply = new StringBuilder();

            httpStartStreamingReply.Append("HTTP/1.0 200 OK\r\n");
            httpStartStreamingReply.Append($"Content-Disposition: inline; filename=\"{fileName}\"\r\n");
            httpStartStreamingReply.Append($"Content-Type: {contentType}\r\n");
            httpStartStreamingReply.Append("Connection: keep-alive\r\n");
            httpStartStreamingReply.Append("\r\n");

            return httpStartStreamingReply.ToString();
        }

        /// <summary>
        /// Is the socket connected?
        /// </summary>
        /// <returns>true if connected, or false</returns>
        public bool IsConnected()
        {
            return Socket != null && Socket.Connected;
        }

        public void Dispose()
        {
            // Force a TCP RST (not a graceful FIN) so any bytes still queued in the stream socket are discarded
            // instead of lingering — otherwise a keep-alive receiver can misread leftover audio as the HTTP
            // headers of the next song ("socket junk"), per the 2026 robust-sender guidance.
            try
            {
                if (Socket != null)
                    Socket.LingerState = new LingerOption(true, 0);
            }
            catch (Exception)
            {
            }

            Socket?.Close();
            Socket?.Dispose();
            Socket = null;
        }

        /// <summary>
        /// What this connection carried, in the form a log reader needs.
        ///
        /// When a receiver stops with a decode error, the first question is whether it hit some limit -
        /// so the answer has to be in the line that reports the loss. With two speakers reading the SAME
        /// stream, comparing their totals says at once whether the stream is at fault or the device: on
        /// 2026-09-05 a soundbar failed twice while an Enchant read the identical bytes for half an hour.
        /// </summary>
        public string Carried()
        {
            var seconds = (DateTime.Now - startedAt).TotalSeconds;
            var megabytes = bytesSent / (1024.0 * 1024.0);
            return $"{megabytes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} MB in " +
                   $"{seconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} s";
        }

        /// <summary>
        /// Get the remote endpoint of the socket.
        /// </summary>
        /// <returns>the remote endpoint</returns>
        public string GetRemoteEndPoint()
        {
            if (Socket == null)
                return string.Empty;

            try
            {
                return (Socket?.RemoteEndPoint?.ToString())!;
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        /// <summary>
        /// Set the socket to use for streaming.
        /// </summary>
        /// <param name="socketIn"></param>
        public void SetDependencies(Socket socketIn, IDevice deviceIn, ILogger loggerIn)
        {
            device = deviceIn;
            logger = loggerIn;
            Socket = socketIn;
            Socket.SendTimeout = 10000;
        }
    }
}
