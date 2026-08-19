namespace Axolotl2D
{
    /// <summary>
    /// Represents a service that simply hosts the game.
    /// </summary>
    /// <param name="game">Game to host.</param>
    internal class SimpleGameHost(
        Game game,
        Microsoft.Extensions.Hosting.IHostApplicationLifetime applicationLifetime) : IGameHost
    {
        private Task? gameLoop;

        /// <summary>
        /// Starts the game.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await game.InitializeGameAsync(cancellationToken).ConfigureAwait(false);
            gameLoop = Task.Run(game.Start, CancellationToken.None);
            _ = gameLoop.ContinueWith(
                _ => applicationLifetime.StopApplication(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Stops the game.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            game.Stop();
            if (gameLoop is not null)
                await gameLoop.ConfigureAwait(false);
        }
    }
}
