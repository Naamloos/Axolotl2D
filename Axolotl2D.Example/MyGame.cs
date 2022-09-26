using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example
{
    internal class MyGame : Axolotl
    {
        #if DEBUG
            const bool DEBUGGING = true;
        #else
            const bool DEBUGGING = false;
        #endif

        public MyGame() : base("My first game", 800, 500, DEBUGGING)
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
