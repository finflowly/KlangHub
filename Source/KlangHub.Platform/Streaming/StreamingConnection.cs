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
        private readonly Thread streamThread;
        private BufferBlock bufferCaptured, bufferSend;
        readonly object bufferSwapSync = new();

        public StreamingConnection()
        {
            audioHeader = new AudioHeader();
            isAudioHeaderSent = false;
            bufferCaptured = new BufferBlock() { Data = new byte[10000000] };
            bufferSend = new BufferBlock() { Data = new byte[10000000] };

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
                    var count = streamer.bufferSend.Used;
                    if (count > 0)
                    {
                        streamer.bufferSend.Used = 0;

                        try
                        {
                            streamer.Socket?.Send(streamer.bufferSend.Data, 0, count, SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            var deviceState = streamer.device?.GetDeviceState();
                            if (deviceState == DeviceState.Playing ||
                                deviceState == DeviceState.Buffering ||
                                deviceState == DeviceState.Paused)
                            {
                                streamer.Dispose();
                                streamer.logger.Log(ex, $"[{DateTime.Now.ToLongTimeString()}] [{streamer.device!.GetHost()}:{streamer.device.GetPort()}] Disconnected Send");
                                streamer.device?.SetDeviceState(DeviceState.ConnectError);
                                streamer.device?.CloseConnection();
                            }
                        }
                    }

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

            // Lag control functionality.
            if (reduceLagThreshold < 1000)
            {
                reduceLagCounter++;
                if (reduceLagCounter > reduceLagThreshold)
                {
                    reduceLagCounter = 0;
                    return;
                }
            }

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
