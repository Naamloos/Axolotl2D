using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Rendering;
using System.Reflection;

namespace Axolotl2D.Example.Bletris;

public sealed class BletrisGame(IServiceProvider services, AssetManager assets, AudioPlayer audio)
    : Game(services)
{
    private SoundAsset? musicAsset;
    private SoundPlayback? music;

    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await assets.LoadEmbeddedAsync<Texture2D>(
            "block", assembly,
            "Axolotl2D.Example.Bletris.Resources.Sprites.logo.png", cancellationToken);
        await assets.LoadEmbeddedAsync<FontAsset>(
            "ui-font", assembly,
            "Axolotl2D.Example.Bletris.Resources.Fonts.ComicMono.ttf", cancellationToken);
        musicAsset = await assets.LoadEmbeddedAsync<SoundAsset>(
            "music", assembly,
            "Axolotl2D.Example.Bletris.Resources.Music.SpaceJazz.wav", cancellationToken);
        OnLoad += StartMusic;
    }

    private void StartMusic() => music = audio.Play(musicAsset!, loop: true, volume: 0.32f);

    protected override void Cleanup()
    {
        OnLoad -= StartMusic;
        music?.Dispose();
    }
}
