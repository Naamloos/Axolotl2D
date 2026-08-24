using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Particles;
using Axolotl2D.Rendering;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Scenes;

public sealed class ParticleScene(
    AssetManager assets,
    Camera2D camera,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input) : ExampleSceneBase(assets)
{
    private InputAction burst = null!;
    private InputAction toggle = null!;
    private ParticleEmitter sparks = null!;
    private ParticleEmitter sprites = null!;
    private ParticleEmitter rotating = null!;

    public override void Load()
    {
        LoadExample("Particles", "#171E2D");
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        burst = input.BindButton("Particle burst", Key.Space);
        toggle = input.BindButton("Toggle emission", Key.P);

        var sparkObject = Instantiate("Primitive sparks");
        sparkObject.Transform.LocalPosition = new Vector2(-230f, 80f);
        sparks = sparkObject.AddComponent<ParticleEmitter>();
        sparks.EmissionRate = 45f;
        sparks.Lifetime = 1.4f;
        sparks.LifetimeVariation = 0.5f;
        sparks.Speed = 150f;
        sparks.SpeedVariation = 80f;
        sparks.Direction = -MathF.PI / 2f;
        sparks.Spread = 1.2f;
        sparks.Acceleration = new Vector2(0f, 180f);
        sparks.StartSize = 14f;
        sparks.EndSize = 2f;
        sparks.StartColor = Color.FromHTML("#FFD166");
        sparks.EndColor = Color.Transparent;

        var spriteObject = Instantiate("Sprite particles");
        spriteObject.Transform.LocalPosition = new Vector2(250f, 80f);
        sprites = spriteObject.AddComponent<ParticleEmitter>();
        sprites.Sprite = new Sprite(assets.Get<Texture2D>("logo"));
        sprites.EmissionRate = 8f;
        sprites.Lifetime = 2.2f;
        sprites.Speed = 90f;
        sprites.SpeedVariation = 35f;
        sprites.Spread = MathF.Tau;
        sprites.StartSize = 48f;
        sprites.EndSize = 8f;
        sprites.AngularVelocity = 2f;
        sprites.StartColor = Color.White;
        sprites.EndColor = Color.Transparent;

        var rotatingObject = Instantiate("Rotating particle emitter");
        rotatingObject.Transform.LocalPosition = new Vector2(0f, 100f);
        rotating = rotatingObject.AddComponent<ParticleEmitter>();
        rotating.EmissionRate = 55f;
        rotating.Lifetime = 1.6f;
        rotating.Speed = 130f;
        rotating.SpeedVariation = 20f;
        rotating.Direction = 0f;
        rotating.Spread = 0.18f;
        rotating.StartSize = 10f;
        rotating.EndSize = 1f;
        rotating.StartColor = Color.FromHTML("#45E6D0");
        rotating.EndColor = Color.Transparent;
    }

    public override void Draw(double frameDelta, double frameRate) =>
        DrawText(spriteBatch, textRenderer,
            $"Primitive, sprite, and rotating emitters | Space burst | P play/stop | Alive: {sparks.AliveCount + sprites.AliveCount + rotating.AliveCount}",
            new Vector2(24f, 70f), 15f);

    protected override void UpdateExample(double deltaTime)
    {
        rotating.Transform.Rotate(1.8f * (float)deltaTime);
        if (burst.WasPressedThisFrame)
        {
            sparks.Emit(80);
            sprites.Emit(18);
            rotating.Emit(50);
        }
        if (toggle.WasPressedThisFrame)
        {
            if (sparks.IsPlaying) { sparks.Stop(); sprites.Stop(); rotating.Stop(); }
            else { sparks.Play(); sprites.Play(); rotating.Play(); }
        }
    }
}
