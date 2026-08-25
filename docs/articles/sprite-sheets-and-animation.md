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

An animation needs at least one frame and a positive frame rate. The FPS constructor remains the shortest path for uniformly timed clips.

For irregular timing, markers, or ping-pong playback, provide timed frames:

```csharp
var attack = new SpriteAnimation(
[
    new SpriteAnimationFrame(sheet[10], 0.08),
    new SpriteAnimationFrame(sheet[11], 0.12, marker: "hit"),
    new SpriteAnimationFrame(sheet[12], 0.20)
], SpriteAnimationPlayback.Once);
```

Playback modes are `Once`, `Loop`, and `PingPong`. `Duration` reports the sum of the forward frame durations. `FramesPerSecond` is zero for a clip whose frame durations are not uniform.

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
animator.Pause();
animator.Resume();
animator.SeekFrame(2);
animator.Restart();
animator.Stop();

string? current = animator.CurrentAnimation;
bool playing = animator.IsPlaying;
int frame = animator.CurrentFrameIndex;
float frameProgress = animator.FrameProgress;
```

Calling `Play` for the animation that is already playing does nothing unless `restart` is true. A non-looping animation stops on its final frame. `Stop` leaves the current frame visible.

`PlaybackSpeed` scales animation time and must be positive. `FrameChanged`, `MarkerReached`, `LoopCompleted`, and `Completed` report playback transitions. The animator advances one frame at a time even when a long update crosses several frames, so markers are not silently skipped:

```csharp
animator.MarkerReached += marker =>
{
    if (marker == "hit") DealDamage();
};
animator.Completed += () => animator.Play("idle");
```

## Prefab clips

Prefab animations may select explicit sheet frames, override individual durations, attach markers, and choose a playback mode. Omitting `frames` preserves the previous behavior of using the whole sheet:

```json
{
  "texture": "hero",
  "frameWidth": 32,
  "frameHeight": 32,
  "animations": [
    {
      "name": "attack",
      "framesPerSecond": 12,
      "playback": "once",
      "frames": [
        { "index": 8 },
        { "index": 9, "duration": 0.16, "marker": "hit" },
        { "index": 10 }
      ]
    }
  ],
  "play": "attack"
}
```

Animations advance during `Update`, so they follow variable frame time. Animation objects and sprites can be shared among many animators; each component stores its own playback state.
