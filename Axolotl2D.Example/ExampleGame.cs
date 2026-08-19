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
    private SoundAsset? musicAsset;
    private SoundPlayback? music;

    public ExampleGame(IServiceProvider services, AssetManager assets, AudioPlayer audio, ILogger<ExampleGame> logger)
        : base(services, maxDrawRate: 240, maxUpdateRate: 240)
    {
        this.assets = assets;
        this.audio = audio;
        this.logger = logger;
        OnLoad += StartMusic;
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await assets.LoadEmbeddedAsync<Texture2D>(
            "logo", assembly, $"{ResourcePrefix}.Sprites.logo.png", cancellationToken);
        await assets.LoadEmbeddedAsync<FontAsset>(
            "ui-font", assembly, $"{ResourcePrefix}.Fonts.ComicMono.ttf", cancellationToken);
        musicAsset = await assets.LoadEmbeddedAsync<SoundAsset>(
            "music", assembly, $"{ResourcePrefix}.Music.SpaceJazz.wav", cancellationToken);
        logger.LogInformation("Loaded typed texture, font, and sound assets");
    }

    private void StartMusic() =>
        music = audio.Play(musicAsset!, loop: true, volume: 0.35f);

    protected override void Cleanup()
    {
        OnLoad -= StartMusic;
        music?.Dispose();
        logger.LogInformation("Example game shut down");
    }
}
