using Axolotl2D.Cef;
using Axolotl2D.Scenes;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes
{
    public class ExampleScene2 : BaseScene
    {
        private IKeyboard? keyboard;
        private readonly ILogger<ExampleScene2> logger;

        private CefBrowserManager browserManager;
        private CefBrowser? browser1;
        private CefBrowser? browser2;
        private CefBrowser? browser3;

        public ExampleScene2(ILogger<ExampleScene2> logger, CefBrowserManager cefBrowserManager)
        {
            this.logger = logger;
            browserManager = cefBrowserManager;
        }

        public override void Load()
        {
            Game.Title = "Scene 2";
            Game.ClearColor = Color.RamptoerismeBlue;
            keyboard = Game.GetKeyboard()!;
            logger.LogInformation("Loaded Example Scene 2");

            browserManager.TryGetBrowser("github", out browser1);
            browserManager.TryGetBrowser("google", out browser2);
            browserManager.TryGetBrowser("discord", out browser3);

            if (browser1 == null || browser2 == null || browser3 == null)
            {
                logger.LogError("Failed to load CEF browsers!");
                return;
            }

            Resize(Game.Viewport);

            browser1.Enable();
            browser2.Enable();
            browser3.Enable();
        }

        public override void Unload()
        {
            logger.LogInformation("Unloaded Example Scene 2");
            browser1?.Disable();
            browser2?.Disable();
            browser3?.Disable();
        }

        public override void Draw(double frameDelta, double frameRate)
        {
            browser1?.Draw();
            browser2?.Draw();
            browser3?.Draw();
        }

        public override void Resize(Vector2 size)
        {
            // set both browsers to half of the screen
            if (browser1 != null)
                browser1.Size = new Vector2(size.X / 2, size.Y / 2);
            if (browser2 != null)
                browser2.Bounds = (new Vector2(size.X / 2, 0), new Vector2(size.X / 2, size.Y / 2));
            if (browser3 != null)
                browser3.Bounds = (new Vector2(0, size.Y / 2), new Vector2(size.X, size.Y / 2));
        }

        private bool? wasKeyPressed = null;
        public override void Update(double frameDelta)
        {
            if (keyboard!.IsKeyPressed(Key.Escape))
            {
                if (wasKeyPressed == false)
                {
                    SceneGameHost.ChangeScene<ExampleScene>();
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
