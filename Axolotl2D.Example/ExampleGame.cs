using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using Axolotl2D.Input;
using Silk.NET.Input;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example
{
    public class ExampleGame : Game
    {
        private const int QUAD_COUNT = 9;
        private const int MOVE_SPEED = 5;

        private float currentXPos = 0;
        private bool goesRight = true;

        private Sprite? sprite;
        private Sprite? sprite2;

        private Mouse? mouse;

        public ExampleGame() : base(maxDrawRate: 240, maxUpdateRate: 240)
        {
            // Set a title for the window
            Title = "Axolotl2D Example";

            // Subscribe to game events
            OnLoad += Load;
            OnUpdate += Update;
            OnDraw += Draw;
            OnResize += Resize;
        }

        public void Draw(double frameDelta, double frameRate)
        {
            for (int i = 0; i < QUAD_COUNT; i++)
            {
                var thisSprite = i % 2 == 0 ? sprite : sprite2;
                thisSprite!.Draw(currentXPos, i * 75, 50, 50);
            }
        }

        public void Load()
        {
            Console.WriteLine("Loaded");
            sprite = new Sprite(this, this.GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!);
            sprite2 = new Sprite(this, this.GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!);
            mouse = GetMouse();
        }

        public void Resize(Vector2D<int> size)
        {
            Console.WriteLine("Resized");
        }

        public void Update(double frameDelta)
        {
            float maxX = WindowWidth - 50;
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

            if(mouse!.LeftButton == MouseKeyState.Click)
                Console.WriteLine($"click!");
            if(mouse!.LeftButton == MouseKeyState.Release)
                Console.WriteLine($"release!");
        }

        public override void Cleanup()
        {
            // unhook events
            OnLoad -= Load;
            OnUpdate -= Update;
            OnDraw -= Draw;
            OnResize -= Resize;
        }
    }
}
