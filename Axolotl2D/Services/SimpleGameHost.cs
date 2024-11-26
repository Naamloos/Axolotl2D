using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    internal class SimpleGameHost : IGameHost
    {
        private Game game;

        public SimpleGameHost(Game game)
        {
            this.game = game;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.Run(() => game.start());

        public Task StopAsync(CancellationToken cancellationToken) => Task.Run(() => game.stop());
    }
}
