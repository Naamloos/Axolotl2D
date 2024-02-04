using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example
{
    internal class Game : BaseGame
    {
        public Game() : base("My first game", 800, 500)
        {

        }

        public override void OnDraw(double frameDelta, double frameRate)
        {
        }

        public override void OnLoad()
        {
            Console.WriteLine("My cool game loaded!");
        }

        public override void OnResize()
        {
        }
    }
}
