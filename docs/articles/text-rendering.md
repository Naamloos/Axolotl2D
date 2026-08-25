# Text Rendering

`TextRenderer.Draw` uses a per-font SDF glyph atlas for printable ASCII, keeping glyph edges sharp across scales and batching glyph instances. Complex Unicode text falls back to exact whole-string Skia shaping and transparent atlas regions. Text uses `SpriteBatch`, so it shares sprite tint, depth, and coordinate behavior.

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

Use `Render` when you want to keep and transform the generated texture yourself. It always uses exact whole-string rasterization rather than SDF glyphs:

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

`TextRenderer.Draw` and `UIText` cache by the `FontAsset` instance, font size, and exact text. Repeating the same combination reuses its atlas region. Atlas pages are recycled between frames using least-recently-used order; a page referenced by the current frame is never recycled early.

`Render` remains an exact-size standalone-texture API and retains its compatibility cache. Prefer `Draw` or `UIText` for frequently changing labels. Font size must be positive; `text` and `font` cannot be null.
