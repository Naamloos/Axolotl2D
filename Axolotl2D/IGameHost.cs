using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D
{
    /// <summary>
    /// Represents a service that hosts the game. Only one instance of this type should exist.
    /// </summary>
    public interface IGameHost : IHostedService
    {
    }
}
