using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Rendering;
using Axolotl2D.Example.Assets;
using Axolotl2D.Packages;
using Axolotl2D.Saving;
using Microsoft.Extensions.Logging;

namespace Axolotl2D.Example.Bletris;

public sealed class BletrisGame(
    IServiceProvider services,
    AssetManager assets,
    AxolotlPackageManager packages,
    AudioPlayer audio,
    SaveGameManager saves,
    ILogger<BletrisGame> logger) : Game(services)
{
    private SoundAsset? musicAsset;
    private SoundPlayback? music;
    private Task pendingSave = Task.CompletedTask;
    public int HighScore { get; private set; }

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await packages.LoadAsync(Path.Combine(AppContext.BaseDirectory, ExampleAssetPackage.FileName),
            ExampleAssetPackage.TrustPolicy(), cancellationToken);
        await assets.LoadPackageAsync<Texture2D>("block", ExampleAssetPackage.Id, "logo", cancellationToken);
        await assets.LoadPackageAsync<FontAsset>("ui-font", ExampleAssetPackage.Id, "ui-font", cancellationToken);
        musicAsset = await assets.LoadPackageAsync<SoundAsset>("music", ExampleAssetPackage.Id, "music", cancellationToken);
        HighScore = (await saves.LoadAsync<BletrisSave>("profile", cancellationToken: cancellationToken))?.HighScore ?? 0;
        OnLoad += StartMusic;
    }

    public void RecordScore(int score)
    {
        if (score <= HighScore) return;
        HighScore = score;
        pendingSave = SaveAfterAsync(pendingSave, score);
    }

    private async Task SaveAfterAsync(Task previous, int highScore)
    {
        await previous.ConfigureAwait(false);
        try { await saves.SaveAsync("profile", new BletrisSave(highScore)).ConfigureAwait(false); }
        catch (Exception exception) { logger.LogError(exception, "Could not save Bletris high score"); }
    }

    private void StartMusic() => music = audio.Play(musicAsset!, loop: true, volume: 0.32f);

    protected override void Cleanup()
    {
        OnLoad -= StartMusic;
        pendingSave.GetAwaiter().GetResult();
        music?.Dispose();
    }
}

internal sealed record BletrisSave(int HighScore);
