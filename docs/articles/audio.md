# Audio Playback

Axolotl2D loads PCM WAVE data into `SoundAsset` and plays it through the DI-registered `AudioPlayer`. The player shares an OpenAL buffer for repeated playback of the same asset.

## Load a sound

Await sound loading from `Game.InitializeAsync` and pass through its cancellation token:

```csharp
await assets.LoadFileAsync<SoundAsset>(
    "jump",
    "Assets/jump.wav",
    cancellationToken);
```

The built-in loader accepts RIFF/WAVE PCM with one or two channels and 8-bit or 16-bit samples. Compressed formats such as MP3 and Ogg Vorbis need a custom asset loader or decoder.

## Play a sound effect

Inject `AudioPlayer` into a scene or component:

```csharp
public sealed class PlayerAudio(
    GameObject gameObject,
    AudioPlayer audioPlayer,
    AssetManager assets) : Component(gameObject)
{
    private SoundPlayback? jumpPlayback;

    public void Jump()
    {
        jumpPlayback?.Dispose();
        jumpPlayback = audioPlayer.Play(
            assets.Get<SoundAsset>("jump"),
            volume: 0.8f);
    }

    public override void OnDestroy() => jumpPlayback?.Dispose();
}
```

`Play` starts immediately and returns a `SoundPlayback`. Volume must be finite and non-negative. Pitch must be positive, and pan ranges from `-1` (left) to `1` (right):

```csharp
audioPlayer.PlayOneShot(jump, volume: 0.8f, pitch: 1.1f, pan: -0.25f);
```

Use `PlayOneShot` when no handle is needed. Finished non-looping sources are detected during the game update and automatically released.

## Control playback

Keep the returned handle when the sound must be paused, resumed, stopped, or looped:

```csharp
SoundPlayback music = audioPlayer.Play(
    assets.Get<SoundAsset>("music"),
    loop: true,
    volume: 0.5f);

music.Pause();
music.Play();
music.Volume = 0.35f;
music.Pitch = 0.95f;
music.Loop = true;
music.Stop();
music.Dispose();
```

`State` reports whether a handle is initial, playing, paused, stopped, or disposed. `Completed` runs when a non-looping source reaches its natural end, immediately before automatic disposal. Explicitly stopped and looping handles remain available until replayed or disposed. Disposing a handle stops playback and releases its OpenAL source.

`AudioPlayer.MasterVolume` controls listener gain without changing individual source volumes. `Muted` preserves that volume while silencing output. `PauseAll`, `ResumeAll`, and `StopAll` control the active set; `ResumeAll` resumes only sources paused by `PauseAll`.

## Spatial audio

Spatial playback positions a source and listener in the same game-defined 2D coordinate system:

```csharp
audioPlayer.ListenerPosition = camera.Position;
audioPlayer.ListenerVelocity = cameraVelocity;

SoundPlayback fire = audioPlayer.PlaySpatial(
    fireSound,
    position: campfire.Transform.Position,
    loop: true,
    referenceDistance: 100f,
    maximumDistance: 1200f,
    rolloffFactor: 1f);

fire.Position = campfire.Transform.Position;
fire.Velocity = campfireVelocity;
```

`ReferenceDistance` is where attenuation begins, `MaximumDistance` clamps the attenuation distance, and `RolloffFactor` controls how quickly gain falls. Axolotl2D uses OpenAL's clamped inverse-distance model. Use mono assets for spatial sources; OpenAL implementations do not spatialize stereo buffers. Non-spatial sources instead expose `Pan`.

`AudioPlayer` is a singleton, so playback can continue across scene changes. A component that owns looping scene music should dispose its `SoundPlayback` in `OnDestroy` if the sound must stop when that scene ends:

```csharp
public override void OnDestroy() => music?.Dispose();
```

Disposing `AudioPlayer` releases every remaining source and shared buffer, then closes the OpenAL context.
