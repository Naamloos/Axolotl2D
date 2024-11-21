using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using Axolotl2D.Input;
using Microsoft.Extensions.Logging;
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

        private float _currentXPos = 0;
        private bool _goesRight = true;

        private Sprite? _sprite1;
        private Sprite? _sprite2;

        private Mouse? _mouse;

        private ILogger<ExampleGame> _logger;

        public ExampleGame(IServiceProvider services, Mouse mouse, ILogger<ExampleGame> logger) 
            : base(services, maxDrawRate: 240, maxUpdateRate: 240) // We want to pass the service provider to the game engine so it can utilize it.
        {
            // Set a title for the window
            Title = "Axolotl2D Example";
            ClearColor = Color.FromHTML("#0088FF");

            // Subscribe to game events.
            OnLoad += Load;
            OnUpdate += Update;
            OnDraw += Draw;
            OnResize += Resize;

            this._logger = logger;
            this._mouse = mouse;
        }

        public void Draw(double frameDelta, double frameRate)
        {
            for (int i = 0; i < QUAD_COUNT; i++)
            {
                var thisSprite = i % 2 == 0 ? _sprite1 : _sprite2;
                thisSprite!.Draw(_currentXPos, i * 75, 50, 50);
            }
        }

        public void Load()
        {
            _sprite1 = Sprite.FromManifestResource(this, "Axolotl2D.Example.Resources.Sprites.mochicat.png");
            _sprite2 = Sprite.FromManifestResource(this, "Axolotl2D.Example.Resources.Sprites.rei.png");

            _logger.LogInformation("Loaded Game");
        }

        public void Resize(Vector2D<int> size)
        {
        }

        public void Update(double frameDelta)
        {
            float maxX = WindowWidth - 50;
            float deltaPosition = MOVE_SPEED * ((float)frameDelta * 60);
            _currentXPos += _goesRight ? deltaPosition : -deltaPosition;

            if (_currentXPos > maxX)
            {
                _goesRight = false;
            }
            else if (_currentXPos < 0)
            {
                _goesRight = true;
            }

            if(_mouse!.LeftButton == MouseKeyState.Click)
                _logger.LogInformation("Mouse Click");
            if (_mouse!.LeftButton == MouseKeyState.Release)
                _logger.LogInformation("Mouse Released");
        }

        public override void Cleanup()
        {
            // unhook events
            OnLoad -= Load;
            OnUpdate -= Update;
            OnDraw -= Draw;
            OnResize -= Resize;

            _logger.LogInformation("Cleaned up events and unloading game...");
        }
    }
}
