using System;

namespace KlangHub.Application.Interfaces
{
    public interface IConfiguration
    {
        void Load(Action<string, string, bool> configurationCallback, ILogger logger);
    }
}