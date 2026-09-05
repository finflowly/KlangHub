using System.Net.Sockets;
using KlangHub.Application;
using KlangHub.Classes;

namespace KlangHub.Streaming.Interfaces
{
    public interface IStreamingConnection
    {
        void SendData(byte[] dataToSend, AudioFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat);
        void SendStartStreamingResponse(SupportedStreamFormat streamFormat);
        bool IsConnected();
        void SetDependencies(Socket socketIn, IDevice deviceIn, ILogger loggerIn);
        string GetRemoteEndPoint();

        /// <summary>How much audio this connection carried and for how long, for the log.</summary>
        string Carried();
        void Dispose();
    }
}