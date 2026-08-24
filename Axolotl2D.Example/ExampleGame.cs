using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Rendering;
using Microsoft.Extensions.Logging;
using Axolotl2D.Example.Assets;
using Axolotl2D.Packages;
using Axolotl2D.Prefabs;

namespace Axolotl2D.Example;

public sealed class ExampleGame : Game
{
    private readonly AssetManager assets;
    private readonly AxolotlPackageManager packages;
    private readonly AudioPlayer audio;
    private readonly ILogger<ExampleGame> logger;
    private SoundAsset? musicAsset;
    private SoundPlayback? music;

    public ExampleGame(IServiceProvider services, AssetManager assets, AxolotlPackageManager packages,
        AudioPlayer audio, ILogger<ExampleGame> logger)
        : base(services, maxDrawRate: 240, maxUpdateRate: 240)
    {
        this.assets = assets;
        this.packages = packages;
        this.audio = audio;
        this.logger = logger;
        OnLoad += StartMusic;
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await packages.LoadAsync(Path.Combine(AppContext.BaseDirectory, ExampleAssetPackage.FileName),
            ExampleAssetPackage.TrustPolicy(), cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("logo", ExampleAssetPackage.Id, "logo", cancellationToken);
        await assets.LoadPackageAsync<FontAsset>("ui-font", ExampleAssetPackage.Id, "ui-font", cancellationToken);
        musicAsset = await assets.LoadPackageAsync<SoundAsset>("music", ExampleAssetPackage.Id, "music", cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("run", ExampleAssetPackage.Id, "run", cancellationToken);
        await assets.LoadPackageAsync<PrefabAsset>("axolotl-cluster", ExampleAssetPackage.Id,
            "prefabs/axolotl-cluster", cancellationToken);
        logger.LogInformation("Loaded typed texture, font, sound, and prefab assets");
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
