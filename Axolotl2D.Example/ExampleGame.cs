using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using Axolotl2D.Input;
using Axolotl2D.Services;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;
using System.Reflection;

namespace Axolotl2D.Example
{
    public class ExampleGame : Game
    {
        private ILogger<ExampleGame> _logger;
        private AssetManager _assetManager;

        public ExampleGame(IServiceProvider services, ILogger<ExampleGame> logger, AssetManager assetManager) 
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
            this._assetManager = assetManager;
        }

        public void Draw(double frameDelta, double frameRate)
        {

        }

        public void Load()
        {
            // preload assets
            _assetManager.LoadSprite("mochicat", Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!);
            _assetManager.LoadSprite("rei", Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!);

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
