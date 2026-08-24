using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Rendering;
using Axolotl2D.Example.Assets;
using Axolotl2D.Packages;

namespace Axolotl2D.Example.Bletris;

public sealed class BletrisGame(IServiceProvider services, AssetManager assets, AxolotlPackageManager packages, AudioPlayer audio)
    : Game(services)
{
    private SoundAsset? musicAsset;
    private SoundPlayback? music;

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await packages.LoadAsync(Path.Combine(AppContext.BaseDirectory, ExampleAssetPackage.FileName),
            ExampleAssetPackage.TrustPolicy(), cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("block", ExampleAssetPackage.Id, "logo", cancellationToken);
        await assets.LoadPackageAsync<FontAsset>("ui-font", ExampleAssetPackage.Id, "ui-font", cancellationToken);
        musicAsset = await assets.LoadPackageAsync<SoundAsset>("music", ExampleAssetPackage.Id, "music", cancellationToken);
        OnLoad += StartMusic;
    }

    private void StartMusic() => music = audio.Play(musicAsset!, loop: true, volume: 0.32f);

    protected override void Cleanup()
    {
        OnLoad -= StartMusic;
        music?.Dispose();
    }
}
