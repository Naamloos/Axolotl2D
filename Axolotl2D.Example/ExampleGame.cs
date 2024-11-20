using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example
{
    internal class ExampleGame : Game
    {
        private SimpleQuad? _quad;
        private SimpleQuad? _quad2;

        public ExampleGame() : base(800, 800, AxolotlColor.RamptoerismeBlue)
        {
            Title = "Axolotl2D Example";
        }

        public override void OnDraw(double frameDelta, double frameRate)
        {
            _quad!.Draw();
            _quad2!.Draw();
        }

        public override void OnLoad()
        {
            Console.WriteLine("Loaded");
            _quad = new SimpleQuad(this);
            _quad2 = new SimpleQuad(this);
            _quad.SetRect(50, 50, 100, 100);
            _quad2.SetRect(200, 200, 100, 100);
        }

        public override void OnResize()
        {
            Console.WriteLine("Resized");
        }

        public override void OnUpdate(double frameDelta)
        {
            // SetRect takes window size into account.
            _quad!.SetRect(50, 50, 100, 100);
            _quad2!.SetRect(200, 200, 100, 100);
        }
    }
}
