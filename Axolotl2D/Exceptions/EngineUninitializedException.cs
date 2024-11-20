using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Exceptions
{
    public class EngineUninitializedException : Exception
    {
        public Game Game { get; }

        internal EngineUninitializedException(string reason, Game game) : base(reason)
        {
            Game = game;
        }
    }
}
