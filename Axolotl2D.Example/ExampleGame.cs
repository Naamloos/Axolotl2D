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
        private ILogger<ExampleGame> _logger;

        public ExampleGame(IServiceProvider services, ILogger<ExampleGame> logger) 
            : base(services, maxDrawRate: 240, maxUpdateRate: 240) // We want to pass the service provider to the game engine so it can utilize it.
        {
            // Set a title for the window
            Title = "Axolotl2D Example";
            ClearColor = Color.FromHTML("#0088FF");

            // Subscribe to game events, if needed outside of the Scene Manager
            OnLoad += Load;
            OnUpdate += Update;
            OnDraw += Draw;
            OnResize += Resize;

            this._logger = logger;
        }

        public void Draw(double frameDelta, double frameRate)
        {

        }

        public void Load()
        {
            _logger.LogInformation("Loaded Game");
        }

        public void Resize(Vector2 size)
        {
        }

        public void Update(double frameDelta)
        {
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
