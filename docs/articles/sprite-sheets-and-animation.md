# Sprite Sheets and Animation

`SpriteSheet` divides a uniform texture atlas into sprites. `SpriteAnimation` describes a timed frame sequence, and `SpriteAnimator` applies named animations to a `SpriteRenderer`.

## Slice a sprite sheet

```csharp
Texture2D texture = assets.Get<Texture2D>("hero");
var sheet = new SpriteSheet(
    texture,
    frameWidth: 32,
    frameHeight: 32,
    margin: 1,
    spacing: 1);
```

Frames are produced in row-major order: left to right, then top to bottom. Access a frame by index or use `Sprites` to select a range:

```csharp
Sprite idle = sheet[0];
IEnumerable<Sprite> walkFrames = sheet.Sprites.Skip(4).Take(6);
```

Frame dimensions must fit at least once inside the texture after margin is applied. Width and height must be positive; margin and spacing cannot be negative.

## Define animations

```csharp
var idleAnimation = new SpriteAnimation(
    sheet.Sprites.Take(4),
    framesPerSecond: 6);

var attackAnimation = new SpriteAnimation(
    sheet.Sprites.Skip(10).Take(5),
    framesPerSecond: 12,
    loop: false);
```

An animation needs at least one frame and a positive frame rate.

## Attach an animator

Add `SpriteRenderer` before `SpriteAnimator`:

```csharp
var renderer = player.AddComponent<SpriteRenderer>();
renderer.Sprite = sheet[0];

var animator = player.AddComponent<SpriteAnimator>();
animator.Add("idle", idleAnimation);
animator.Add("attack", attackAnimation);
animator.Play("idle");
```

`SpriteAnimator.Start` locates the renderer on the same GameObject and throws if none exists. Adding the renderer first also makes the intended dependency clear.

## Control playback

```csharp
animator.Play("attack", restart: true);
animator.Stop();

string? current = animator.CurrentAnimation;
bool playing = animator.IsPlaying;
```

Calling `Play` for the animation that is already playing does nothing unless `restart` is true. A non-looping animation stops on its final frame. `Stop` leaves the current frame visible.

Animations advance during `Update`, so they follow variable frame time. Animation objects and sprites can be shared among many animators; each component stores its own playback state.
