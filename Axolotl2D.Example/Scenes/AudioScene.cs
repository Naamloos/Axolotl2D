using Axolotl2D.Assets;
using Axolotl2D.Audio;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Silk.NET.Input;
using System.Buffers.Binary;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class AudioScene(
    AssetManager assets,
    AudioPlayer audio,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    PrimitiveBatch primitives,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private InputAction playSpatial = null!;
    private InputAction playPanned = null!;
    private InputAction toggleMute = null!;
    private SoundAsset tone = null!;
    private double elapsed;
    private Vector2 emitterPosition;

    public override void Load()
    {
        LoadExample("Audio playback and spatial sound", "#241B35");
        tone = CreateTone();
        audio.ListenerPosition = Vector2.Zero;
        playSpatial = input.BindButton("Play spatial sound", Key.Space);
        playPanned = input.BindButton("Play panned sound", Key.P);
        toggleMute = input.BindButton("Toggle master mute", Key.M);
    }

    protected override void UpdateExample(double deltaTime)
    {
        elapsed += deltaTime;
        emitterPosition = new Vector2(MathF.Sin((float)elapsed) * 420f, 0f);
        if (playSpatial.WasPressedThisFrame)
            audio.PlayOneShotSpatial(tone, emitterPosition, referenceDistance: 80f, maximumDistance: 700f);
        if (playPanned.WasPressedThisFrame)
            audio.PlayOneShot(tone, pan: emitterPosition.X / 420f);
        if (toggleMute.WasPressedThisFrame)
            audio.Muted = !audio.Muted;
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        primitives.DrawLine(new Vector2(-420f, 0f), new Vector2(420f, 0f), Color.DarkGray,
            2f, CoordinateSpace.World);
        primitives.FillCircle(Vector2.Zero, 12f, Color.Cyan, CoordinateSpace.World);
        primitives.FillCircle(emitterPosition, 16f, Color.Orange, CoordinateSpace.World);
        DrawText(spriteBatch, textRenderer, "Space: spatial tone | P: stereo-pan tone | M: master mute",
            new Vector2(24f, 76f), 17f);
        DrawText(spriteBatch, textRenderer,
            $"Listener: (0, 0) | emitter X: {emitterPosition.X:0} | muted: {audio.Muted}",
            new Vector2(24f, 108f), 16f, Color.LightGray);
    }

    public override void Unload() => audio.Muted = false;

    private static SoundAsset CreateTone()
    {
        const int sampleRate = 22050;
        const float duration = 0.22f;
        var sampleCount = (int)(sampleRate * duration);
        var samples = new byte[sampleCount * sizeof(short)];
        for (var index = 0; index < sampleCount; index++)
        {
            var fade = 1f - index / (float)sampleCount;
            var value = (short)(MathF.Sin(index * MathF.Tau * 660f / sampleRate) * short.MaxValue * 0.25f * fade);
            BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(index * sizeof(short), sizeof(short)), value);
        }
        return new SoundAsset(samples, sampleRate, channels: 1, bitsPerSample: 16);
    }
}
