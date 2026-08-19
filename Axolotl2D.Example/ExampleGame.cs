using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Rendering;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Axolotl2D.Example;

public sealed class ExampleGame : Game
{
    private const string ResourcePrefix = "Axolotl2D.Example.Resources";
    private readonly AssetManager assets;
    private readonly AudioPlayer audio;
    private readonly ILogger<ExampleGame> logger;
    private SoundPlayback? music;

    public ExampleGame(IServiceProvider services, AssetManager assets, AudioPlayer audio, ILogger<ExampleGame> logger)
        : base(services, maxDrawRate: 240, maxUpdateRate: 240)
    {
        this.assets = assets;
        this.audio = audio;
        this.logger = logger;
        OnLoad += LoadAssets;
    }

    private void LoadAssets()
    {
        var assembly = Assembly.GetExecutingAssembly();
        assets.LoadEmbeddedAsync<Texture2D>("logo", assembly, $"{ResourcePrefix}.Sprites.logo.png").AsTask().GetAwaiter().GetResult();
        assets.LoadEmbeddedAsync<FontAsset>("ui-font", assembly, $"{ResourcePrefix}.Fonts.ComicMono.ttf").AsTask().GetAwaiter().GetResult();
        var song = assets.LoadEmbeddedAsync<SoundAsset>("music", assembly, $"{ResourcePrefix}.Music.SpaceJazz.wav").AsTask().GetAwaiter().GetResult();
        music = audio.Play(song, loop: true, volume: 0.35f);
        logger.LogInformation("Loaded typed texture, font, and sound assets");
    }

    protected override void Cleanup()
    {
        OnLoad -= LoadAssets;
        music?.Dispose();
        logger.LogInformation("Example game shut down");
    }
}
