# Particle Emitters

`ParticleEmitter` is a GameObject component that emits, updates, and draws short-lived 2D particles. It supports continuous emission, immediate bursts, deterministic random sequences, local or world simulation, sprite rendering, and a primitive-circle fallback.

## Create an emitter

Add and configure the component during scene loading:

```csharp
using Axolotl2D.Particles;

var fire = Instantiate("Fire");
fire.Transform.LocalPosition = new Vector2(160, 240);

var emitter = fire.AddComponent<ParticleEmitter>();
emitter.Sprite = new Sprite(assets.Get<Texture2D>("spark"));
emitter.MaxParticles = 400;
emitter.EmissionRate = 80f;
emitter.Lifetime = 0.8f;
emitter.LifetimeVariation = 0.2f;
emitter.Speed = 90f;
emitter.SpeedVariation = 25f;
emitter.Direction = -MathF.PI / 2f;
emitter.Spread = 0.7f;
emitter.Acceleration = new Vector2(0, -20f);
emitter.StartSize = 14f;
emitter.EndSize = 2f;
emitter.StartColor = Color.Orange;
emitter.EndColor = Color.Transparent;
```

`Direction` and `Spread` use radians. `Lifetime`, variation, speed, size, color, rotation, and angular velocity are sampled or interpolated by the component. The emitter never exceeds `MaxParticles`.

If `Sprite` is `null`, each particle is drawn as a filled primitive circle. This is convenient for prototypes and simple effects. Assign a small texture for large particle counts because textured particles need one quad each while filled primitive circles use several spans.

## Play, stop, and burst

`PlayOnStart` defaults to `true`. Control the emitter at runtime with:

```csharp
emitter.Stop();              // live particles finish
emitter.Play();              // resume continuous emission
emitter.Emit(32);            // immediate burst
emitter.Stop(clear: true);   // stop and remove every live particle
```

`SetRandomSeed(seed)` restarts its random sequence. Use it before `Emit` when an effect must be reproducible.

## Simulation and coordinate space

`ParticleSimulationSpace.World` makes emitted particles independent of later emitter movement. The initial direction follows the emitter's world rotation.

`ParticleSimulationSpace.Local` stores particles relative to the emitter. Moving or rotating the GameObject moves the entire live effect:

```csharp
emitter.SimulationSpace = ParticleSimulationSpace.Local;
```

`Space` selects rendering coordinates. It defaults to `CoordinateSpace.World`; choose `CoordinateSpace.Screen` for menu or HUD effects. Acceleration uses the selected simulation space and updates with scaled component `deltaTime`, so pausing the scene pauses the effect.

Particle submission happens from the component `Render` callback. Scenes already own the surrounding `SpriteBatch.Begin` and `End` calls.
