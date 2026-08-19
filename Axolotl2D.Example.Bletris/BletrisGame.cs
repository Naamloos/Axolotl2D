using Axolotl2D.Assets;
using Axolotl2D.Rendering;
using System.Reflection;

namespace Axolotl2D.Example.Bletris;

public sealed class BletrisGame(IServiceProvider services, AssetManager assets)
    : Game(services)
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken = default) =>
        await assets.LoadEmbeddedAsync<Texture2D>(
            "block", Assembly.GetExecutingAssembly(),
            "Axolotl2D.Example.Bletris.Resources.Sprites.logo.png", cancellationToken);

    protected override void Cleanup() { }
}
