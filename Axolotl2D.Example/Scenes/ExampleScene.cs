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

        private float currentXPosition = 0;
        private bool movingRight = true;
        private float currentRotation = 0;

        private BaseDrawable? axolotlLogo;

        private IKeyboard? keyboard;

        private readonly ILogger<ExampleScene> logger;

        private readonly ExampleGame game;
        private readonly SpriteManager assetManager;

        public ExampleScene(ExampleGame game, ILogger<ExampleScene> logger, SpriteManager assetManager)
        {
            this.game = game;
            this.keyboard = game.GetKeyboard()!;
            this.game.Title = "Scene 1";

            this.logger = logger;
            this.assetManager = assetManager;
        }

        public override void Draw(double frameDelta, double frameRate)
        {
            for (int i = 0; i < QUAD_COUNT; i++)
            {
                axolotlLogo!.Rotation = currentRotation;
                axolotlLogo!.Draw(new Vector2(currentXPosition, i * 150), new Vector2(160, 100));
            }

            // Squish the width and height at random speeds
            float squishWidth = 160 + (float)(Math.Sin(currentRotation * 2) * 80);
            float squishHeight = 100 + (float)(Math.Cos(currentRotation * 3) * 50);
            axolotlLogo!.Draw(new Vector2(300, 300), new Vector2(squishWidth, squishHeight));
        }

        public override void Load()
        {
            // get streams for resources
            using var mochiCat = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!;
            using var rei = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!;

            // It is not recommended to load Sprites any time a scene is initialized, as it can cause memory leaks.
            // At this moment it is not possible to do this any other way. This will be fixed in the future.
            this.assetManager.TryGetSprite("logo", out axolotlLogo);

            keyboard = game.GetKeyboard();

            logger.LogInformation("Loaded Example Scene");
        }

        public override void Resize(Vector2 size)
        {

        }

        public override void Unload()
        {
            logger.LogInformation("Unloaded Example Scene");
        }

        private bool? wasKeyPressed = null;

        public override void Update(double frameDelta)
        {
            float maxX = game.Viewport.X - 160;
            float deltaPosition = MOVE_SPEED * ((float)frameDelta * 60);
            currentXPosition += movingRight ? deltaPosition : -deltaPosition;

            currentRotation += 0.01f;

            if (currentXPosition > maxX)
            {
                movingRight = false;
            }
            else if (currentXPosition < 0)
            {
                movingRight = true;
            }

            if (keyboard!.IsKeyPressed(Key.Space))
                game.ClearColor = Color.FromHTML("#00BBFF");
            else
                game.ClearColor = Color.FromHTML("#0088FF");

            if (keyboard!.IsKeyPressed(Key.Escape))
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
