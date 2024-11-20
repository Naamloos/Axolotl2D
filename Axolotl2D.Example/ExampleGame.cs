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
        private const int QUAD_COUNT = 5;
        private SimpleQuad? quad;

        public ExampleGame() : base(800, 800, AxolotlColor.RamptoerismeBlue)
        {
            Title = "Axolotl2D Example";
        }

        public override void OnDraw(double frameDelta, double frameRate)
        {
            if(quad == null)
            {
                return;
            }

            for (int i = 0; i < QUAD_COUNT; i++)
            {
                quad.SetRect(x, i * 75, 50, 50);
                quad.Draw();
            }
        }

        public override void OnLoad()
        {
            Console.WriteLine("Loaded");
            quad = new SimpleQuad(this);
        }

        public override void OnResize()
        {
            Console.WriteLine("Resized");
        }

        public override void OnUpdate(double frameDelta)
        {
            
        }

        int x = 0;
        bool goesRight = true;
        const int SPEED = 15;

        public override void OnFixedUpdate(double frameDelta)
        {
            int maxX = GetWidth() - 50;
            x += goesRight? SPEED : -SPEED;

            if (x > maxX)
            {
                goesRight = false;
            }
            else if (x < 0)
            {
                goesRight = true;
            }
        }
    }
}
