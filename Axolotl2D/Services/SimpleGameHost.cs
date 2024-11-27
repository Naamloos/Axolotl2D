using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    /// <summary>
    /// Represents a service that simply hosts the game.
    /// </summary>
    /// <param name="game">Game to host.</param>
    internal class SimpleGameHost(Game game) : IGameHost
    {
        /// <summary>
        /// Starts the game.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken) => Task.Run(() => game.Start(), cancellationToken);

        /// <summary>
        /// Stops the game.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken) => Task.Run(() => game.Stop(), cancellationToken);
    }
}
