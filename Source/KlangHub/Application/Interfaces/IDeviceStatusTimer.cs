using System;

namespace KlangHub.Application.Interfaces
{
    public interface IDeviceStatusTimer
    {
        void StartPollingDevice(Action onGetStatus);
    }
}