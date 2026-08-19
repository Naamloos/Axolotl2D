# Text Rendering

`TextRenderer` shapes and rasterizes scalable font text into transparent `Texture2D` assets. Text then uses `SpriteBatch`, so it shares sprite tint, depth, and coordinate behavior.

## Load a font

Await font loading through `AssetManager` from `Game.InitializeAsync`:

```csharp
await assets.LoadFileAsync<FontAsset>(
    "ui",
    "Assets/Inter-Regular.ttf",
    cancellationToken);
```

`FontAssetLoader` supports TrueType and OpenType input accepted by SkiaSharp.

## Draw text in a scene

Inject `TextRenderer`, `SpriteBatch`, and `AssetManager` into the scene:

```csharp
public sealed class HudScene(
    TextRenderer textRenderer,
    SpriteBatch spriteBatch,
    AssetManager assets) : BaseScene
{
    public override void Draw(double frameDelta, double frameRate)
    {
        textRenderer.Draw(
            spriteBatch,
            assets.Get<FontAsset>("ui"),
            "Score: 1200",
            fontSize: 24,
            position: new Vector2(16, 16),
            color: Color.White,
            space: CoordinateSpace.Screen,
            depth: 1f);
    }
}
```

Text defaults to `CoordinateSpace.Screen` and uses a top-left sprite origin, which suits UI placement. Select `CoordinateSpace.World` for labels that should pan and zoom with the camera.

Scenes already have an open sprite batch during `Draw`. Do not call `Begin` or `End` around `TextRenderer.Draw` there.

## Render once, draw as a sprite

Use `Render` when you want to keep and transform the generated texture yourself:

```csharp
Texture2D labelTexture = textRenderer.Render(font, "Ready", 36);
var label = new Sprite(labelTexture) { Origin = new Vector2(0.5f) };

spriteBatch.Draw(
    label,
    new Vector2(game.Viewport.X / 2f, 80),
    tint: Color.White,
    space: CoordinateSpace.Screen);
```

## Text cache

`TextRenderer` caches by the `FontAsset` instance, font size, and exact text. Repeating the same combination reuses its texture. A different score string, size, or font creates another cached texture.

Prefer stable labels for frequently drawn UI. For rapidly changing text, update only when its value changes rather than calling `Render` speculatively. The current text cache has no per-entry eviction API, so avoid creating an unbounded stream of unique strings. Font size must be positive; `text` and `font` cannot be null.
