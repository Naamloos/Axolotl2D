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

`Play` starts immediately and returns a `SoundPlayback`. Volume is clamped to zero or higher.

## Control playback

Keep the returned handle when the sound must be paused, resumed, stopped, or looped:

```csharp
SoundPlayback music = audioPlayer.Play(
    assets.Get<SoundAsset>("music"),
    loop: true,
    volume: 0.5f);

music.Pause();
music.Play();
music.Stop();
music.Dispose();
```

Dispose every playback handle when its owner no longer needs it. The current API does not automatically dispose a source when a one-shot sound reaches its end. Disposing a handle stops playback and releases its OpenAL source. Disposing `AudioPlayer` disposes every remaining playback, releases shared buffers, and closes the OpenAL context.

`AudioPlayer` is a singleton, so playback can continue across scene changes. A component that owns looping scene music should dispose its `SoundPlayback` in `OnDestroy` if the sound must stop when that scene ends:

```csharp
public override void OnDestroy() => music?.Dispose();
```
