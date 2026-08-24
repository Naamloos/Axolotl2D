# Asset Management

`AssetManager` loads typed assets through dependency-injected loaders and caches them by asset type and string key. Loading is independent of rendering, so game code works with `Texture2D`, `SoundAsset`, and `FontAsset` rather than file-format-specific APIs.

## Built-in asset types

| Asset type | Loader | Supported input |
| --- | --- | --- |
| `Texture2D` | `TextureAssetLoader` | PNG, JPEG, BMP, and other formats supported by stb_image |
| `SoundAsset` | `SoundAssetLoader` | RIFF/WAVE PCM, mono or stereo, 8-bit or 16-bit |
| `FontAsset` | `FontAssetLoader` | TrueType and OpenType fonts supported by SkiaSharp |
| `PrefabAsset` | `PrefabAssetLoader` | Versioned `.axprefab` JSON hierarchies |

The built-in loaders are registered by `AddAxolotl2D()` and by both game-host registration methods.

Prefab component asset references resolve through already loaded asset keys. See [Data Prefabs](prefabs.md) for authoring, component registration, and package integration.

## Load from files

Load assets in `Game.InitializeAsync`. The game host awaits initialization before the window and first scene start:

```csharp
public sealed class MyGame : Game
{
    private readonly AssetManager assets;

    public MyGame(IServiceProvider services, AssetManager assets) : base(services)
    {
        this.assets = assets;
    }

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await assets.LoadFileAsync<Texture2D>(
            "player", "Assets/player.png", cancellationToken);
        await assets.LoadFileAsync<SoundAsset>(
            "jump", "Assets/jump.wav", cancellationToken);
        await assets.LoadFileAsync<FontAsset>(
            "ui", "Assets/Inter-Regular.ttf", cancellationToken);
    }

    protected override void Cleanup() { }
}
```

`LoadFileAsync<T>()` owns and closes the file stream after the loader finishes.

## Load from streams or resources

Use `LoadAsync<T>()` when another system supplies a stream. The caller retains ownership of that stream. Use `LoadEmbeddedAsync<T>()` for an assembly resource:

```csharp
await assets.LoadEmbeddedAsync<Texture2D>(
    "logo",
    typeof(MyGame).Assembly,
    "MyGame.Assets.logo.png",
    cancellationToken);
```

## Retrieve and unload

```csharp
Texture2D player = assets.Get<Texture2D>("player");

if (assets.TryGet<SoundAsset>("jump", out var jump))
{
    audioPlayer.Play(jump!);
}

assets.Unload<Texture2D>("temporary-background");
```

Keys are separated by asset type, so a texture and sound can use the same key. Loading the same type-and-key pair again returns the first cached asset rather than replacing it. `Get<T>()` throws when no match exists; `TryGet<T>()` supports optional content.

Unloading disposes assets that implement `IDisposable`. Disposing `AssetManager` unloads every cached asset.

## Add a custom asset loader

Implement `IAssetLoader<TAsset>` and register it with DI:

```csharp
public sealed record Dialogue(IReadOnlyList<string> Lines);

public sealed class DialogueAssetLoader : IAssetLoader<Dialogue>
{
    public async ValueTask<Dialogue> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return new Dialogue(text.Split('\n'));
    }
}
```

```csharp
services.AddSingleton<IAssetLoader<Dialogue>, DialogueAssetLoader>();
```

The loader can request its own dependencies through constructor injection. Keep loaders focused on decoding source data into an asset; let rendering and gameplay services decide how to use it.
