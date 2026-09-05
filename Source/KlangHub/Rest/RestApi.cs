using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KlangHub.Application;
using KlangHub.Application.Interfaces;
using KlangHub.Streaming;

namespace KlangHub.Rest
{
    public class RestApi : IDisposable
    {
        public ManualResetEvent allDone = new ManualResetEvent(false);
        private Action<Socket, string, IDevices, ILogger, Action>? onConnectCallback;
        private Socket listener = null!;
        private string? ip;
        private int port;
        private ILogger logger = null!;
        private IDevices devices = null!;
        private Action restartRecording = null!;

        /// <summary>
        /// Start listening for new API request.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="onConnectCallbackIn"></param>
        public void StartListening(IPAddress ipAddress, Action<Socket, string, IDevices, ILogger, Action> onConnectCallbackIn, ILogger loggerIn, IDevices devicesIn, Action restartRecordingIn)
        {
            if (ipAddress == null || onConnectCallbackIn == null)
                return;

            logger = loggerIn;
            onConnectCallback = onConnectCallbackIn;
            devices = devicesIn;
            restartRecording = restartRecordingIn;

            try
            {
                // Loopback, not the LAN address that is passed in.
                //
                // Every endpoint here changes something - /start, /stop, /volume/<device>/<0-100>,
                // /togglemute - and none of them asks who is calling. Bound to the LAN address, anyone on
                // the network could read the speaker list, with names and addresses, and set every
                // speaker in the house to 100 at three in the morning. Worse, all of it answers a plain
                // GET, so no network access was needed at all: an <img> tag on any web page the owner
                // happened to open would do it, because the browser sends the request from inside the
                // house and does not need to read the reply for the volume to change.
                //
                // Opening this up again is a feature with a design - a token issued on first run, a Host
                // header that is checked, state changes moved to POST - not a default.
                var localEndPoint = new IPEndPoint(IPAddress.Loopback, 27272);
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(localEndPoint);
                listener.Listen(100);
                var endPoint = (IPEndPoint?)listener.LocalEndPoint;
                if (endPoint != null)
                {
                    ip = endPoint.Address?.ToString();
                    port = endPoint.Port;
                    logger.Log(string.Format("RestApi from {0}:{1}", ip, port));

                    while (true)
                    {
                        allDone.Reset();
                        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                        allDone.WaitOne();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(ex, "RestApi.StartListening");
            }
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void StopListening()
        {
            try
            {
                listener.Close();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>How long a caller is given to send a whole request before its connection is dropped.</summary>
        private static readonly TimeSpan RequestPatience = TimeSpan.FromSeconds(10);

        /// <summary>Past this, whatever is arriving is not one of our requests.</summary>
        private const int LargestSensibleRequest = 16 * 1024;

        /// <summary>The request line, with anything the caller may have put after it left out.</summary>
        private static string FirstLine(string request)
        {
            var end = request.IndexOfAny(new[] { '\r', '\n' });
            var line = end < 0 ? request : request[..end];
            return line.Length > 200 ? line[..200] : line;
        }

        /// <summary>
        /// Accept the connection.
        /// </summary>
        /// <param name="asyncResult"></param>
        private void AcceptCallback(IAsyncResult asyncResult)
        {
            if (asyncResult == null || asyncResult.AsyncState == null)
                return;

            try
            {
                allDone.Set();

                var listener = (Socket)asyncResult.AsyncState;
                var handlerSocket = listener.EndAccept(asyncResult);

                // A caller that opens a connection and then says nothing used to hold it open for as long
                // as it liked, against a backlog of 100.
                handlerSocket.ReceiveTimeout = (int)RequestPatience.TotalMilliseconds;
                handlerSocket.SendTimeout = (int)RequestPatience.TotalMilliseconds;

                var state = new StateObject { workSocket = handlerSocket };

                state.buffer = new byte[StateObject.bufferSize];
                handlerSocket.BeginReceive(state.buffer, 0, StateObject.bufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
            catch (Exception ex)
            {
                logger.Log(ex, "RestApi.AcceptCallback");
            }
        }

        /// <summary>
        /// Read incoming bytes.
        /// </summary>
        private void ReadCallback(IAsyncResult asyncResult)
        {
            if (asyncResult == null || asyncResult.AsyncState == null || onConnectCallback == null)
                return;

            try
            {
                var state = (StateObject)asyncResult.AsyncState;
                var handlerSocket = state.workSocket!;

                var bytesRead = handlerSocket.EndReceive(asyncResult);
                if (bytesRead <= 0)
                {
                    // The caller went away mid-request. Nothing was closing the socket on this path.
                    handlerSocket.Close();
                    return;
                }

                state.receiveBuffer.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                if (state.receiveBuffer.ToString().IndexOf("\r\n\r\n") >= 0)
                {
                    // The request line only, and on one line of its own. Logging the whole request wrote
                    // every header the caller chose to send - newlines included - straight into the log,
                    // so anything reading that log could be shown entries that were never written.
                    logger.Log($"RestApi: {FirstLine(state.receiveBuffer.ToString())}");
                    onConnectCallback?.Invoke(handlerSocket, state.receiveBuffer.ToString(), devices, logger, restartRecording);
                    handlerSocket.Close();
                }
                else if (state.receiveBuffer.Length > LargestSensibleRequest)
                {
                    // No blank line and already past anything a request of ours could be.
                    handlerSocket.Close();
                }
                else
                {
                    // Not all data received. Get more.
                    handlerSocket.BeginReceive(state.buffer, 0, StateObject.bufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
            }
            catch (Exception ex)
            {
                logger.Log(ex, "RestApi.ReadCallback");
                try { ((StateObject)asyncResult.AsyncState).workSocket?.Close(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        protected virtual void Dispose(bool cleanupAll)
        {
            allDone?.Dispose();
            listener?.Dispose();
        }
    }
}