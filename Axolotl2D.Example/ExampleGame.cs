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
        private const int QUAD_COUNT = 9;
        private const int MOVE_SPEED = 15;

        private float currentXPos = 0;
        private bool goesRight = true;

        private SimpleQuad? quad;

        public ExampleGame() : base(800, 650, AxolotlColor.RamptoerismeBlue)
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
                quad.SetRect(currentXPos, i * 75, 50, 50);
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
            float maxX = GetWidth() - 50;
            float deltaPosition = MOVE_SPEED * ((float)frameDelta * 60);
            currentXPos += goesRight ? deltaPosition : -deltaPosition;

            if (currentXPos > maxX)
            {
                goesRight = false;
            }
            else if (currentXPos < 0)
            {
                goesRight = true;
            }
        }
    }
}
