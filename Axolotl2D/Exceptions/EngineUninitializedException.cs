using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Exceptions
{
    /// <summary>
    /// Thrown when an action is performed that required the engine to be initialized, but it is not.
    /// </summary>
    public class EngineUninitializedException : Exception
    {
        /// <summary>
        /// The game that the engine is running.
        /// </summary>
        public Game Game { get; }

        internal EngineUninitializedException(string reason, Game game) : base(reason)
        {
            Game = game;
        }
    }
}
