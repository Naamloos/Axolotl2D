using Axolotl2D.Audio;
using Axolotl2D.Cef;
using Axolotl2D.Drawable;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Reflection;

namespace Axolotl2D.Example
{
    public class ExampleGame : Game
    {
        private readonly ILogger<ExampleGame> logger;
        private readonly SpriteManager assetManager;
        private readonly CefBrowserManager browserManager;

        private readonly Song _song;

        public ExampleGame(IServiceProvider services, ILogger<ExampleGame> logger, SpriteManager assetManager, CefBrowserManager cefBrowserManager,
            AudioPlayer audioPlayer) 
            : base(services, maxDrawRate: 240, maxUpdateRate: 240) // We want to pass the service provider to the game engine so it can utilize it.
        {
            // Subscribe to game events, if needed outside of the Scene Manager
            // It is recommended to hook OnLoad to load assets
            OnLoad += Load;

            this.logger = logger;
            this.assetManager = assetManager;
            this.browserManager = cefBrowserManager;

            _song = audioPlayer.LoadSong(Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Music.SpaceJazz.wav")!);
        }

        public void Load()
        {
            // preload assets
            assetManager.LoadSprite("logo", Assembly.GetEntryAssembly()!.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.logo.png")!);

            browserManager.RegisterBrowser("github", "https://naamloos.github.io/Axolotl2D.Webtest/");
            browserManager.RegisterBrowser("google", "https://google.com");
            browserManager.RegisterBrowser("discord", "https://discord.com/app");

            logger.LogInformation("Loaded Game");

            _song.Play();
        }

        protected override void Cleanup()
        {
            // unhook events
            OnLoad -= Load;

            logger.LogInformation("Cleaned up events and unloading game...");
        }
    }
}
