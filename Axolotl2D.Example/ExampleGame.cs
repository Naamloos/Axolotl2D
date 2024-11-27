using Axolotl2D.Audio;
using Axolotl2D.Cef;
using Axolotl2D.Drawable;
using Axolotl2D;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System.Numerics;
using System.Reflection;

namespace Axolotl2D.Example
{
    public class ExampleGame : Game
    {
        private readonly ILogger<ExampleGame> _logger;
        private readonly SpriteManager _assetManager;
        private readonly CefBrowserManager _cefBrowserManager;

        private readonly Song _song;

        public ExampleGame(IServiceProvider services, ILogger<ExampleGame> logger, SpriteManager assetManager, CefBrowserManager cefBrowserManager,
            AudioPlayer audioPlayer) 
            : base(services, maxDrawRate: 240, maxUpdateRate: 240) // We want to pass the service provider to the game engine so it can utilize it.
        {
            // Subscribe to game events, if needed outside of the Scene Manager
            // It is recommended to hook OnLoad to load assets
            OnLoad += Load;

            this._logger = logger;
            this._assetManager = assetManager;
            this._cefBrowserManager = cefBrowserManager;

            _song = audioPlayer.LoadSong(Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Music.SpaceJazz.wav")!);
        }

        public void Load()
        {
            // preload assets
            _assetManager.LoadSprite("mochicat", Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!);
            _assetManager.LoadSprite("rei", Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!);

            _cefBrowserManager.RegisterBrowser("github", "https://naamloos.github.io/Axolotl2D.Webtest/");
            _cefBrowserManager.RegisterBrowser("google", "https://google.com");
            _cefBrowserManager.RegisterBrowser("discord", "https://discord.com/app");

            _logger.LogInformation("Loaded Game");

            _song.Play();
        }

        public override void Cleanup()
        {
            // unhook events
            OnLoad -= Load;

            _logger.LogInformation("Cleaned up events and unloading game...");
        }
    }
}
