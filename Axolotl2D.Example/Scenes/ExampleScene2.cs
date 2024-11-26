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

        public ExampleScene2(ExampleGame game, ILogger<ExampleScene2> logger)
        {
            game.Title = "Scene 2";
            game.ClearColor = Color.FromHTML("#FF00FF");

            _game = game;
            _logger = logger;
        }

        public override void Load()
        {
            _keyboard = _game.GetKeyboard()!;
            _logger.LogInformation("Loaded Example Scene 2");
        }

        public override void Unload()
        {
            _logger.LogInformation("Unloaded Example Scene 2");
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
