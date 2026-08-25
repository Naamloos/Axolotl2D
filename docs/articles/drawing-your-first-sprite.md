# Drawing Sprites, Animation, and Text

## Batched sprites

Scenes open and close `SpriteBatch` around component rendering and `BaseScene.Draw`. A `SpriteRenderer` submits its sprite during that open batch:

```csharp
var gameObject = Instantiate("Logo");
gameObject.Transform.LocalPosition = new Vector2(200, 100);
gameObject.Transform.LocalScale = new Vector2(0.5f);
gameObject.AddComponent<SpriteRenderer>().Sprite =
    new Sprite(assets.Get<Texture2D>("logo"));
```

You can inject `SpriteBatch` into a scene for draws that do not need a component:

```csharp
public override void Draw(double deltaTime, double frameRate)
{
    spriteBatch.Draw(sprite, new Vector2(40, 40), space: CoordinateSpace.Screen);
}
```

The batch keeps draw order by `depth` and combines adjacent submissions that use the same texture into one GPU submission.

## Sprite sheets and animation

`SpriteSheet` slices a uniform atlas in row-major order. Attach `SpriteAnimator` after `SpriteRenderer`:

```csharp
var sheet = new SpriteSheet(assets.Get<Texture2D>("hero"), 32, 32);
var renderer = player.AddComponent<SpriteRenderer>();
renderer.Sprite = sheet[0];

var animator = player.AddComponent<SpriteAnimator>();
animator.Add("walk", new SpriteAnimation(sheet.Sprites.Take(6), 10));
animator.Play("walk");
```

## Text

`TextRenderer` shapes text with the loaded font, caches it in a shared transparent atlas, and submits the region through `SpriteBatch`, so text follows the same tint, depth, and coordinate rules as sprites.

```csharp
textRenderer.Draw(
    spriteBatch,
    assets.Get<FontAsset>("ui"),
    "Score: 1200",
    24,
    new Vector2(16, 16),
    Color.White,
    CoordinateSpace.Screen);
```

## Next steps

See [Sprites and Sprite Batching](sprites-and-sprite-batching.md) for origins, matrix draws, depth ordering, and manual batches. Continue with [Camera and Coordinate Systems](camera-and-coordinate-systems.md), [Transforms and Hierarchies](transforms-and-hierarchies.md), and [Sprite Sheets and Animation](sprite-sheets-and-animation.md).
