using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Services
{
    internal class SimpleGameHost(Game game) : IGameHost
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.Run(() => game.Start(), cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.Run(() => game.Stop(), cancellationToken);
    }
}
