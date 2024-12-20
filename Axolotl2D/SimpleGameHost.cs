﻿namespace Axolotl2D
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
