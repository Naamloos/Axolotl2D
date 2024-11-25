using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using Axolotl2D.Input;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example
{
    public class ExampleGame : Game
    {
        private const int QUAD_COUNT = 9;
        private const int MOVE_SPEED = 5;

        private float _currentXPos = 0;
        private bool _goesRight = true;

        private BaseDrawable? _object1;
        private BaseDrawable? _object2;
        private BaseDrawable? _object3;

        private Mouse? _mouse;
        private IKeyboard? _keyboard;

        private ILogger<ExampleGame> _logger;

        public ExampleGame(IServiceProvider services, ILogger<ExampleGame> logger) 
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
        }

        public void Draw(double frameDelta, double frameRate)
        {
            for (int i = 0; i < QUAD_COUNT; i++)
            {
                BaseDrawable? thisSprite;
                if (i % 3 == 0)
                {
                    thisSprite = _object1;
                }
                else if (i % 3 == 1)
                {
                    thisSprite = _object2;
                }
                else
                {
                    thisSprite = _object3;
                }
                thisSprite!.Draw(new Vector2(_currentXPos, i * 74), new Vector2(50, 50));
            }
        }

        public void Load()
        {
            // get streams for resources
            using var mochiCat = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!;
            using var rei = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!;

            _object1 = new Sprite(this, mochiCat, new Vector2(0, 0), new Vector2(50, 50));
            _object2 = new Sprite(this, rei, new Vector2(0, 0), new Vector2(50, 50));
            _object3 = new SimpleQuad(this, new Vector2(0,0), new Vector2(50, 50));

            _mouse = GetMouse();
            _keyboard = GetKeyboard();

            _logger.LogInformation("Loaded Game");
        }

        public void Resize(Vector2 size)
        {
        }

        public void Update(double frameDelta)
        {
            float maxX = Viewport.X - 50;
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

            if (_mouse.LeftButton == MouseKeyState.Held || _keyboard.IsKeyPressed(Key.Space))
                ClearColor = Color.Red;
            else
                ClearColor = Color.FromHTML("#0088FF");
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
