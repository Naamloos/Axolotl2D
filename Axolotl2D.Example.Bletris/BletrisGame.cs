using Axolotl2D.Assets;
using Axolotl2D.Rendering;
using System.Reflection;

namespace Axolotl2D.Example.Bletris;

public sealed class BletrisGame(IServiceProvider services, AssetManager assets)
    : Game(services)
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await assets.LoadEmbeddedAsync<Texture2D>(
            "block", assembly,
            "Axolotl2D.Example.Bletris.Resources.Sprites.logo.png", cancellationToken);
        await assets.LoadEmbeddedAsync<FontAsset>(
            "ui-font", assembly,
            "Axolotl2D.Example.Bletris.Resources.Fonts.ComicMono.ttf", cancellationToken);
    }

    protected override void Cleanup() { }
}
