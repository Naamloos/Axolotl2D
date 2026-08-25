# Sprites and Sprite Batching

Axolotl2D represents image data with `Texture2D`, a drawable region with `Sprite`, and queued drawing with `SpriteBatch`. GPU textures are uploaded lazily when first drawn.

## Textures and sprites

Load a texture through `AssetManager`, then create a sprite:

```csharp
Texture2D texture = assets.Get<Texture2D>("player");
var sprite = new Sprite(texture);
```

A sprite can select part of its texture:

```csharp
var icon = new Sprite(texture, new TextureRegion(32, 0, 16, 16));
```

`Sprite.Origin` is normalized within the selected region. Its default value, `(0.5, 0.5)`, places the origin at the center. Use `(0, 0)` for the top-left or `(0.5, 1)` for the bottom-center:

```csharp
sprite.Origin = new Vector2(0.5f, 1f);
```

## Render with a component

`SpriteRenderer` uses its GameObject's world transform:

```csharp
var player = Instantiate("Player");
player.Transform.LocalPosition = new Vector2(320, 180);

var renderer = player.AddComponent<SpriteRenderer>();
renderer.Sprite = new Sprite(assets.Get<Texture2D>("player"));
renderer.Tint = Color.White;
renderer.Depth = 0.5f;
renderer.Space = CoordinateSpace.World;
```

Changing the Transform moves, rotates, and scales the submitted sprite. Setting `Space` to `Screen` bypasses the camera, which is useful for UI objects.

## Draw directly

Inject `SpriteBatch` into a scene for one-off draws:

```csharp
public sealed class HudScene(SpriteBatch spriteBatch, AssetManager assets) : BaseScene
{
    private Sprite heart = null!;

    public override void Load() =>
        heart = new Sprite(assets.Get<Texture2D>("heart"));

    public override void Draw(double frameDelta, double frameRate)
    {
        spriteBatch.Draw(
            heart,
            position: new Vector2(24, 24),
            size: new Vector2(32, 32),
            tint: Color.White,
            space: CoordinateSpace.Screen,
            depth: 1f);
    }
}
```

Scenes call `SpriteBatch.Begin()` before rendering components and call `End()` after `BaseScene.Draw`. Do not open another batch inside those callbacks.

Outside the scene pipeline, a batch can be managed explicitly:

```csharp
spriteBatch.Begin(camera);
spriteBatch.Draw(sprite, position);
spriteBatch.End();
```

Calling `Draw` without `Begin`, beginning twice, or ending an unopened batch throws `InvalidOperationException`.

## Transform overload

Use the matrix overload when the draw already has a composed transform:

```csharp
Matrix3x2 transform =
    Matrix3x2.CreateScale(2f) *
    Matrix3x2.CreateRotation(MathF.PI / 4f) *
    Matrix3x2.CreateTranslation(200, 100);

spriteBatch.Draw(sprite, transform, Color.White, CoordinateSpace.World, 0.25f);
```

Rotation values are in radians.

## Build an atlas

Pack unrelated source textures before their first draw when they should share GPU submissions:

```csharp
TextureAtlas atlas = new TextureAtlasBuilder()
    .Add("player", assets.Get<Texture2D>("player"))
    .Add("heart", assets.Get<Texture2D>("heart"))
    .Build();

Sprite player = atlas.GetSprite("player");
Sprite heart = atlas.GetSprite("heart");
```

The builder adds edge padding for linear filtering. Pass a same-size normal map to `Add` to build a matching normal atlas. Source textures must still have CPU pixel data; render-target textures cannot be packed.

## Instancing, arrays, and spatial indexing

Built-in sprites use GPU instancing. Custom shaders retain the legacy per-vertex layout for compatibility.

Use `TextureArray2D` for equal-sized images that must remain separate layers:

```csharp
var array = new TextureArray2D(textures);
Sprite sprite = array.GetSprite(layer: 0);
```

Adjacent sprites from one array batch across layer changes. Large static worlds can opt into a `SpatialSpriteIndex`; add sprites once, then call `DrawVisible(spriteBatch, camera)` during drawing.

## Ordering and batching

At `End`, commands are ordered by ascending `depth`, then by submission order. Adjacent commands that use the same texture, texture array, and shader are submitted together. Off-camera world sprites are rejected before sorting and instance generation. Grouping sprites from the same atlas, array, and shader at the same depth reduces state changes while retaining stable order.

See [Sprite Sheets and Animation](sprite-sheets-and-animation.md) for atlas slicing, [Camera and Coordinate Systems](camera-and-coordinate-systems.md) for world and screen drawing, and [Custom Shaders](custom-shaders.md) for shader scopes.
