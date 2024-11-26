using Axolotl2D.Cef;
using Axolotl2D.Entities;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example.Scenes
{
    public class ExampleScene2 : BaseScene
    {
        private readonly ExampleGame _game;
        private IKeyboard? _keyboard;
        private readonly ILogger<ExampleScene2> _logger;

        private CefBrowserManager _cefBrowserManager;
        private CefBrowser? _cef1;
        private CefBrowser? _cef2;

        public ExampleScene2(ExampleGame game, ILogger<ExampleScene2> logger, CefBrowserManager cefBrowserManager)
        {
            game.Title = "Scene 2";
            game.ClearColor = Color.FromHTML("#FFaB6A");

            _game = game;
            _logger = logger;
            _cefBrowserManager = cefBrowserManager;
        }

        public override void Load()
        {
            _keyboard = _game.GetKeyboard()!;
            _logger.LogInformation("Loaded Example Scene 2");

            _cefBrowserManager.TryGetBrowser("github", out _cef1);
            _cefBrowserManager.TryGetBrowser("google", out _cef2);

            if (_cef1 == null || _cef2 == null)
            {
                _logger.LogError("Failed to load CEF browsers!");
                return;
            }

            Resize(_game.Viewport);

            _cef1.Enable();
            _cef2.Enable();
        }

        public override void Unload()
        {
            _logger.LogInformation("Unloaded Example Scene 2");
            _cef1?.Disable();
            _cef2?.Disable();
        }

        public override void Draw(double frameDelta, double frameRate)
        {
            _cef1?.Draw();
            _cef2?.Draw();
        }

        public override void Resize(Vector2 size)
        {
            // set both browsers to half of the screen
            if (_cef1 != null)
                _cef1.Size = new Vector2(size.X / 2, size.Y);
            if (_cef2 != null)
                _cef2.Bounds = (new Vector2(size.X / 2, 0), new Vector2(size.X / 2, size.Y));
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
