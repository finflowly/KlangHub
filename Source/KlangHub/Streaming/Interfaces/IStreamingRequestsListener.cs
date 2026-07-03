using KlangHub.Application.Interfaces;
using System;
using System.Net;
using System.Net.Sockets;

namespace KlangHub.Streaming.Interfaces
{
    public interface IStreamingRequestsListener
    {
        void StartListening(IPAddress ipAddress, Action<Socket, string> onConnectCallbackIn, ILogger logger);
        void StopListening();
        string GetStreamimgUrl();
        void Dispose();
    }
}