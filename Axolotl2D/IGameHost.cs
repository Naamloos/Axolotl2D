using Microsoft.Extensions.Hosting;

namespace Axolotl2D
{
    /// <summary>
    /// Represents a service that hosts the game. Only one instance of this type should exist.
    /// </summary>
    public interface IGameHost : IHostedService
    {
    }
}
