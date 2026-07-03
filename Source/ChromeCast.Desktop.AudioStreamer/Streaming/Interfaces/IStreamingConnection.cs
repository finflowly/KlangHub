using System.Net.Sockets;
using KlangHub.Application;
using KlangHub.Application.Interfaces;
using KlangHub.Classes;
using NAudio.Wave;

namespace KlangHub.Streaming.Interfaces
{
    public interface IStreamingConnection
    {
        void SendData(byte[] dataToSend, WaveFormat format, int reduceLagThreshold, SupportedStreamFormat streamFormat);
        void SendStartStreamingResponse();
        bool IsConnected();
        void SetDependencies(Socket socketIn, IDevice deviceIn, ILogger loggerIn);
        string GetRemoteEndPoint();
        void Dispose();
    }
}