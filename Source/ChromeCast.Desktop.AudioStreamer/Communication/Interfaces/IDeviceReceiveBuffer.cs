using System;
using KlangHub.ProtocolBuffer;

namespace KlangHub.Communication.Interfaces
{
    public interface IDeviceReceiveBuffer
    {
        void OnReceive(byte[] data);
        void SetCallback(Action<CastMessage> onReceiveMessage);
    }
}