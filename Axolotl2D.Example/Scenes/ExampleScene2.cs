using Axolotl2D.Cef;
using Axolotl2D.Scenes;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes
{
    public class ExampleScene2 : BaseScene
    {
        private IKeyboard? _keyboard;
        private readonly ILogger<ExampleScene2> _logger;

        private CefBrowserManager _cefBrowserManager;
        private CefBrowser? _cef1;
        private CefBrowser? _cef2;
        private CefBrowser? _cef3;

        public ExampleScene2(ILogger<ExampleScene2> logger, CefBrowserManager cefBrowserManager)
        {
            _logger = logger;
            _cefBrowserManager = cefBrowserManager;
        }

        public override void Load()
        {
            Game.Title = "Scene 2";
            Game.ClearColor = Color.RamptoerismeBlue;
            _keyboard = Game.GetKeyboard()!;
            _logger.LogInformation("Loaded Example Scene 2");

            _cefBrowserManager.TryGetBrowser("github", out _cef1);
            _cefBrowserManager.TryGetBrowser("google", out _cef2);
            _cefBrowserManager.TryGetBrowser("discord", out _cef3);

            if (_cef1 == null || _cef2 == null || _cef3 == null)
            {
                _logger.LogError("Failed to load CEF browsers!");
                return;
            }

            Resize(Game.Viewport);

            _cef1.Enable();
            _cef2.Enable();
            _cef3.Enable();
        }

        public override void Unload()
        {
            _logger.LogInformation("Unloaded Example Scene 2");
            _cef1?.Disable();
            _cef2?.Disable();
            _cef3?.Disable();
        }

        public override void Draw(double frameDelta, double frameRate)
        {
            _cef1?.Draw();
            _cef2?.Draw();
            _cef3?.Draw();
        }

        public override void Resize(Vector2 size)
        {
            // set both browsers to half of the screen
            if (_cef1 != null)
                _cef1.Size = new Vector2(size.X / 2, size.Y / 2);
            if (_cef2 != null)
                _cef2.Bounds = (new Vector2(size.X / 2, 0), new Vector2(size.X / 2, size.Y / 2));
            if (_cef3 != null)
                _cef3.Bounds = (new Vector2(0, size.Y / 2), new Vector2(size.X, size.Y / 2));
        }

        private bool? wasKeyPressed = null;
        public override void Update(double frameDelta)
        {
            if (_keyboard!.IsKeyPressed(Key.Escape))
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
