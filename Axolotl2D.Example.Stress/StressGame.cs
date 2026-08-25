using Axolotl2D.Assets;
using Axolotl2D.Example.Assets;
using Axolotl2D.Packages;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace Axolotl2D.Example.Stress;

public sealed class StressGame : Game
{
    private readonly AssetManager assets;
    private readonly AxolotlPackageManager packages;
    private readonly ILogger<StressGame> logger;

    public StressGame(IServiceProvider services, AssetManager assets, AxolotlPackageManager packages,
        ILogger<StressGame> logger)
        : base(services, new GameWindowOptions
        {
            Title = "Axolotl2D Stress",
            Size = new Vector2(1280f, 720f),
            MaximumDrawRate = 500d,
            MaximumUpdateRate = 240d,
            VSync = false,
            ShowFramerateInTitle = true
        })
    {
        this.assets = assets;
        this.packages = packages;
        this.logger = logger;
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await packages.LoadAsync(Path.Combine(AppContext.BaseDirectory, ExampleAssetPackage.FileName),
            ExampleAssetPackage.TrustPolicy(), cancellationToken);
        await assets.LoadPackageAsync<FontAsset>("stress-font", ExampleAssetPackage.Id, "ui-font", cancellationToken);
        logger.LogInformation("Stress-test font loaded");
    }

    protected override void Cleanup() { }
}
