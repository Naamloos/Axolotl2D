using Axolotl2D.Drawable;
using Axolotl2D.Scenes;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes
{
    [DefaultScene]
    public class ExampleScene : BaseScene
    {
        private const int QUAD_COUNT = 4;
        private const int MOVE_SPEED = 5;

        private float _currentXPos = 0;
        private bool _goesRight = true;
        private float _currentRotation = 0;

        private BaseDrawable? _logo;

        private IMouse? _mouse;
        private IKeyboard? _keyboard;

        private readonly ILogger<ExampleScene> _logger;

        private readonly ExampleGame _game;
        private readonly SpriteManager _assetManager;

        public ExampleScene(ExampleGame game, ILogger<ExampleScene> logger, SpriteManager assetManager)
        {
            this._game = game;
            this._keyboard = game.GetKeyboard()!;
            this._game.Title = "Scene 1";

            this._logger = logger;
            this._assetManager = assetManager;
        }

        public override void Draw(double frameDelta, double frameRate)
        {
            for (int i = 0; i < QUAD_COUNT; i++)
            {
                _logo!.Rotation = _currentRotation;
                _logo!.Draw(new Vector2(_currentXPos, i * 150), new Vector2(160, 100));
            }

            // Squish the width and height at random speeds
            float squishWidth = 160 + (float)(Math.Sin(_currentRotation * 2) * 80);
            float squishHeight = 100 + (float)(Math.Cos(_currentRotation * 3) * 50);
            _logo!.Draw(new Vector2(300, 300), new Vector2(squishWidth, squishHeight));
        }

        public override void Load()
        {
            // get streams for resources
            using var mochiCat = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!;
            using var rei = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!;

            // It is not recommended to load Sprites any time a scene is initialized, as it can cause memory leaks.
            // At this moment it is not possible to do this any other way. This will be fixed in the future.
            this._assetManager.TryGetSprite("logo", out _logo);

            _mouse = _game.GetMouse();
            _keyboard = _game.GetKeyboard();

            _logger.LogInformation("Loaded Example Scene");
        }

        public override void Resize(Vector2 size)
        {

        }

        public override void Unload()
        {
            _logger.LogInformation("Unloaded Example Scene");
        }

        private bool? wasKeyPressed = null;

        public override void Update(double frameDelta)
        {
            float maxX = _game.Viewport.X - 160;
            float deltaPosition = MOVE_SPEED * ((float)frameDelta * 60);
            _currentXPos += _goesRight ? deltaPosition : -deltaPosition;

            _currentRotation += 0.01f;

            if (_currentXPos > maxX)
            {
                _goesRight = false;
            }
            else if (_currentXPos < 0)
            {
                _goesRight = true;
            }

            if (_keyboard!.IsKeyPressed(Key.Space))
                _game.ClearColor = Color.FromHTML("#00BBFF");
            else
                _game.ClearColor = Color.FromHTML("#0088FF");

            if (_keyboard!.IsKeyPressed(Key.Escape))
            {
                if (wasKeyPressed == false)
                {
                    SceneGameHost.ChangeScene<ExampleScene2>();
                }
                wasKeyPressed = true;
            }
            else
            {
                wasKeyPressed = false;
            }
        }
    }
}
