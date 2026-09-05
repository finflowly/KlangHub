using KlangHub.Core.Models;
﻿using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using KlangHub.Discover;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using System.Threading;
using KlangHub.Communication.Interfaces;
using KlangHub.ProtocolBuffer;

namespace KlangHub.Communication
{
    public class DeviceConnection : IDeviceConnection, IDisposable
    {
        private Func<string> getHost = null!;
        private Func<int> getPort = null!;
        private Action<DeviceState, string> setDeviceState = null!;
        private Action<CastMessage> onReceiveMessage = null!;
        private Action<Action> startTask = null!;
        private readonly ILogger logger;
        private readonly IDeviceReceiveBuffer deviceReceiveBuffer;
        private const int bufferSize = 2048;
        private TcpClient? tcpClient;
        private SslStream sslStream = null!;
        private byte[]? receiveBuffer;
        private volatile DeviceConnectionState state;
        private IAsyncResult currentAsyncResult = null!;
        private volatile bool IsDisposed = false;

        /// <summary>
        /// Messages waiting to go out, and the gate that serialises writing them.
        /// <para>
        /// This was a single field holding the last message, written from whichever thread happened to
        /// call: the heartbeat reply from the receive thread, a volume change from the interface, a
        /// status poll from the timer, a reconnect from a pool task. Two of them arriving together meant
        /// one simply overwrote the other and was never sent - and two threads reaching
        /// <c>sslStream.Write</c> at once meant the device was handed two half-frames plaited together.
        /// A queue and one gate fix both: nothing is lost, and one message goes out at a time.
        /// </para>
        /// </summary>
        private readonly object sendGate = new object();
        private readonly Queue<byte[]> pendingSends = new Queue<byte[]>();

        /// <summary>
        /// A device that is not taking messages must not make us hold them all. Cast control messages are
        /// status and volume: the newest is what matters, so the oldest goes over the side.
        /// </summary>
        private const int MostPendingSends = 64;

        public DeviceConnection(ILogger loggerIn)
        {
            logger = loggerIn;
            deviceReceiveBuffer = new DeviceReceiveBuffer();
            deviceReceiveBuffer.SetCallback(OnReceiveMessage);
        }

        /// <summary>
        /// Make a connection with the device for the control messages.
        /// </summary>
        private void Connect()
        {
            if (tcpClient != null && tcpClient.Client != null && tcpClient.Connected)
                return;

            Close();

            try
            {
                IsDisposed = false;
                var host = getHost();
                var port = getPort();

                BeginConnectTo(host, port);

                // The wait handle is not ours to close. It used to be closed in a finally, which on the
                // timeout path disposed the handle of a BeginConnect that was still running - and the
                // callback then reached for it and got an ObjectDisposedException instead of a connection.
                if (!currentAsyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
                {
                    CloseConnection();
                    throw new TimeoutException();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    state = DeviceConnectionState.Error;
                    setDeviceState?.Invoke(DeviceState.ConnectError, null!);
                    var host = getHost?.Invoke();
                    logger.Log($"ex [{host}]: Connect {ex.Message}");
                    CloseConnection();
                }
                catch (Exception innerEx)
                {
                    logger.Log($"ex: Connect (while handling a failure) {innerEx.Message}");
                }
            }
        }

        /// <summary>
        /// Begin the TCP connect. For an IPv6 literal, use an address-family-specific client and connect via
        /// the IPAddress overload so the scope survives correctly; clear the %zone for a ULA/global address
        /// (the mDNS-supplied scope names the wrong local interface and caused the historical WSAEADDRNOTAVAIL),
        /// while a link-local address keeps its scope. Real DNS names/IPv4 fall through to the string overload.
        /// </summary>
        private void BeginConnectTo(string host, int port)
        {
            if (IPAddress.TryParse(host, out var address))
            {
                if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0 && !address.IsIPv6LinkLocal)
                    address = new IPAddress(address.GetAddressBytes());

                tcpClient = new TcpClient(address.AddressFamily);
                tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                currentAsyncResult = tcpClient.BeginConnect(address, port, new AsyncCallback(ConnectCallback), tcpClient);
            }
            else
            {
                tcpClient = new TcpClient();
                tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                currentAsyncResult = tcpClient.BeginConnect(host, port, new AsyncCallback(ConnectCallback), tcpClient);
            }
        }

        /// <summary>
        /// Close the connection with the device when it's connected.
        /// </summary>
        private void Close()
        {
            if (tcpClient == null || tcpClient.Client == null || !tcpClient.Connected)
                return;

            try
            {
                if (state != DeviceConnectionState.Connecting)
                {
                    CloseConnection();
                }
            }
            catch (Exception ex)
            {
                logger.Log($"ex: Close {ex.Message}");
            }
        }

        /// <summary>
        /// Setup a ssl stream, start receiving and send pending messages when a connection has been made.
        /// </summary>
        /// <param name="ar"></param>
        private void ConnectCallback(IAsyncResult ar)
        {
            if (tcpClient == null || tcpClient.Client == null)
                return;

            try
            {
                if (ar == currentAsyncResult)
                {
                    tcpClient.EndConnect(ar);
                    sslStream = new SslStream(tcpClient.GetStream(), false, new RemoteCertificateValidationCallback(DontValidateServerCertificate), null);
                    var host = getHost?.Invoke();
                    sslStream.AuthenticateAsClient(Ipv4Recovery.Normalize(host), new X509CertificateCollection(), SslProtocols.Tls12, true);
                    StartReceive();
                    DoSendMessage();
                    state = DeviceConnectionState.Connected;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    state = DeviceConnectionState.Error;
                    setDeviceState?.Invoke(DeviceState.ConnectError, null!);
                    var host = getHost?.Invoke();
                    logger.Log($"ex [{host}]: ConnectCallback {ex.Message}");
                    CloseConnection();
                }
                catch (Exception innerEx)
                {
                    logger.Log($"ex: ConnectCallback (while handling a failure) {innerEx.Message}");
                }
            }
        }

        /// <summary>
        /// Return true if there's a connection with the device.
        /// </summary>
        public bool IsConnected()
        {
            return state.Equals(DeviceConnectionState.Connected);
        }

        /// <summary>
        /// Send a message to a device. 
        /// If the device is not connected, a connection is made first.
        /// </summary>
        /// <param name="send">the message</param>
        public void SendMessage(byte[] send)
        {
            if (send == null || send.Length == 0)
                return;

            startTask(() => {
                lock (sendGate)
                {
                    if (pendingSends.Count >= MostPendingSends)
                        pendingSends.Dequeue();
                    pendingSends.Enqueue(send);
                }

                if (tcpClient != null &&
                    tcpClient.Client != null &&
                    tcpClient.Connected &&
                    state == DeviceConnectionState.Connected)
                {
                    DoSendMessage();
                }
                else
                {
                    if (state != DeviceConnectionState.Connecting)
                    {
                        state = DeviceConnectionState.Connecting;
                        Connect();
                    }
                }
            });
        }

        /// <summary>
        /// Do send the message.
        /// </summary>
        private void DoSendMessage()
        {
            if (tcpClient == null || tcpClient.Client == null || !tcpClient.Connected)
                return;

            // One writer at a time. Two threads inside SslStream.Write hand the device two frames
            // interleaved, which it cannot parse and does not complain about - it simply stops answering.
            lock (sendGate)
            {
                while (pendingSends.Count > 0)
                {
                    if (state != DeviceConnectionState.Connected || IsDisposed)
                        return;

                    var message = pendingSends.Dequeue();
                    try
                    {
                        sslStream.Write(message);
                        sslStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        // Logged, not swallowed into a console this application does not have. The
                        // message is already off the queue: retrying a write to a stream that just
                        // failed is how a send loop starts.
                        logger.Log($"ex [{SafeHost()}]: DoSendMessage {ex.Message}");
                        CloseConnection();
                        return;
                    }
                }
            }
        }

        /// <summary>The device's address for a log line, without letting the lookup itself throw.</summary>
        private string SafeHost()
        {
            try { return getHost?.Invoke() ?? "?"; }
            catch (Exception) { return "?"; }
        }

        /// <summary>
        /// Start receiving messages from the device.
        /// </summary>
        private void StartReceive()
        {
            if (IsDisposed)
                return;

            try
            {
                receiveBuffer = new byte[bufferSize];
                sslStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, DataReceived, sslStream);
            }
            catch (Exception ex)
            {
                logger.Log($"ex [{SafeHost()}]: StartReceive {ex.Message}");
                CloseConnection();
            }
        }

        /// <summary>
        /// Received data from the device.
        /// </summary>
        private void DataReceived(IAsyncResult ar)
        {
            if (ar == null || deviceReceiveBuffer == null || receiveBuffer == null || IsDisposed)
                return;

            SslStream stream = (SslStream)ar.AsyncState!;
            int byteCount = -1;
            try
            {
                byteCount = stream.EndRead(ar);
            }
            catch (Exception ex)
            {
                logger.Log($"ex [{SafeHost()}]: DataReceived {ex.Message}");
                CloseConnection();
            }
            if (byteCount == 0)
            {
                // Zero from EndRead is the device closing the connection properly - it was switched off,
                // or it dropped us. Nothing here treated that as anything: it slept half a second on an
                // I/O completion thread and read again, for ever, while `state` stayed Connected and
                // IsConnected() went on telling the rest of the program the device was there.
                logger.Log($"in [{SafeHost()}]: the device closed the connection.");
                state = DeviceConnectionState.Disconnected;
                setDeviceState?.Invoke(DeviceState.Closed, null!);
                CloseConnection();
                return;
            }

            if (byteCount > 0)
                deviceReceiveBuffer.OnReceive(receiveBuffer.Take(byteCount).ToArray());

            StartReceive();
        }

        /// <summary>
        /// Callback for the receive buffer, a complete message is received.
        /// </summary>
        /// <param name="castMessage">the message that's received</param>
        private void OnReceiveMessage(CastMessage castMessage)
        {
            if (IsDisposed)
                return;

            onReceiveMessage?.Invoke(castMessage);
        }

        /// <summary>
        /// Dispose the connection.
        /// </summary>
        private void CloseConnection()
        {
            Dispose(true);
        }

        /// <summary>
        /// Dispose the connection.
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
            IsDisposed = true;
            try
            {
                tcpClient?.Close();
                sslStream?.Close();
                sslStream?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Log($"ex: Dispose {ex.Message}");
            }
        }

        /// <summary>
        /// Accepts whatever certificate the Cast device presents.
        /// <para>
        /// <b>Why this has to be here.</b> Every Cast device carries a certificate issued by Google to
        /// that individual unit, chained to a Cast root that is in no public trust store and is not
        /// published. There is nothing on this machine that could validate it, and no way to obtain
        /// something that could: refusing unknown issuers here does not make the connection safer, it
        /// makes it impossible. Every Cast sender does this, Google's own included.
        /// </para>
        /// <para>
        /// <b>What it costs.</b> Somebody already on the network who can answer for the device's address
        /// - by poisoning ARP, or by replying to the mDNS query first - can terminate this connection
        /// themselves and read or alter the whole control channel: what is playing, the stream URL, the
        /// volume. It cannot reach the audio, which is a separate connection, and it needs a foothold on
        /// the network first.
        /// </para>
        /// <para>
        /// <b>What limits it.</b> This callback is attached to this one <see cref="SslStream"/> and
        /// nothing else. There is deliberately no ServicePointManager or HttpClientHandler equivalent
        /// anywhere in the project, so the update check and every other outbound connection still
        /// validate normally. It is private for the same reason: it is not a helper to be reused.
        /// </para>
        /// <para>
        /// The fix, when it comes, is to remember each device's certificate the first time we see it and
        /// object when it changes - which closes this without needing anybody's root.
        /// </para>
        /// </summary>
        private bool DontValidateServerCertificate(object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        /// <summary>
        /// Set callbacks.
        /// </summary>
        public void SetCallback(Func<string> getHostIn, Func<int> getPortIn, Action<DeviceState, string> setDeviceStateIn, Action<CastMessage> onReceiveMessageIn, Action<Action> startTaskIn)
        {
            getHost = getHostIn;
            getPort = getPortIn;
            setDeviceState = setDeviceStateIn;
            onReceiveMessage = onReceiveMessageIn;
            startTask = startTaskIn;
        }
 
        public void ReConnect()
        {
            state = DeviceConnectionState.Disconnected;
            Dispose(true);
            Connect();
        }
    }
}
