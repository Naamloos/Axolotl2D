using Axolotl2D.GameObjects;
using Axolotl2D.Rendering;
using System.Numerics;

namespace Axolotl2D.Particles;

/// <summary>Emits, simulates, and draws lightweight 2D particles.</summary>
public sealed class ParticleEmitter(
    GameObject gameObject,
    SpriteBatch spriteBatch,
    PrimitiveBatch primitives) : Component(gameObject)
{
    private readonly List<Particle> particles = [];
    private Random random = new();
    private double emissionAccumulator;

    public Sprite? Sprite { get; set; }
    public CoordinateSpace Space { get; set; } = CoordinateSpace.World;
    public ParticleSimulationSpace SimulationSpace { get; set; } = ParticleSimulationSpace.World;
    public int MaxParticles { get; set; } = 1000;
    public float EmissionRate { get; set; } = 10f;
    public float Lifetime { get; set; } = 1f;
    public float LifetimeVariation { get; set; }
    public float Speed { get; set; } = 100f;
    public float SpeedVariation { get; set; }
    public float Direction { get; set; } = -MathF.PI / 2f;
    public float Spread { get; set; } = MathF.Tau;
    public Vector2 Acceleration { get; set; }
    public float StartSize { get; set; } = 8f;
    public float EndSize { get; set; }
    public Color StartColor { get; set; } = Color.White;
    public Color EndColor { get; set; } = Color.Transparent;
    public float StartRotation { get; set; }
    public float AngularVelocity { get; set; }
    public float Depth { get; set; }
    public bool PlayOnStart { get; set; } = true;
    public bool IsPlaying { get; private set; }
    public int AliveCount => particles.Count;

    public override void Start() => IsPlaying = PlayOnStart;

    public override void Update(double deltaTime)
    {
        ValidateConfiguration();
        var elapsed = (float)deltaTime;
        if (IsPlaying && EmissionRate > 0f)
        {
            emissionAccumulator += elapsed * EmissionRate;
            var count = Math.Min((int)emissionAccumulator, MaxParticles - particles.Count);
            if (count > 0)
            {
                Emit(count);
                emissionAccumulator -= count;
            }
            else if (particles.Count >= MaxParticles)
            {
                emissionAccumulator = Math.Min(emissionAccumulator, 1d);
            }
        }

        for (var index = particles.Count - 1; index >= 0; index--)
        {
            var particle = particles[index];
            particle.Age += elapsed;
            if (particle.Age >= particle.Lifetime)
            {
                particles.RemoveAt(index);
                continue;
            }

            particle.Velocity += Acceleration * elapsed;
            particle.Position += particle.Velocity * elapsed;
            particle.Rotation += AngularVelocity * elapsed;
            particles[index] = particle;
        }
    }

    public override void Render()
    {
        foreach (var particle in particles)
        {
            var progress = particle.Age / particle.Lifetime;
            var size = MathF.Max(0f, float.Lerp(StartSize, EndSize, progress));
            if (size <= 0f)
                continue;

            var position = particle.Position;
            var rotation = particle.Rotation;
            if (SimulationSpace == ParticleSimulationSpace.Local)
            {
                position = Transform.TransformPoint(position);
                rotation += Transform.Rotation;
            }

            var color = Lerp(StartColor, EndColor, progress);
            if (Sprite is not null)
                spriteBatch.Draw(Sprite, position, new Vector2(size), rotation, color, Space, Depth);
            else
                primitives.FillCircle(position, size / 2f, color, Space, Depth);
        }
    }

    /// <summary>Starts continuous emission without clearing live particles.</summary>
    public void Play() => IsPlaying = true;

    /// <summary>Stops continuous emission and optionally removes live particles.</summary>
    public void Stop(bool clear = false)
    {
        IsPlaying = false;
        emissionAccumulator = 0d;
        if (clear)
            particles.Clear();
    }

    /// <summary>Emits an immediate burst, capped by <see cref="MaxParticles"/>.</summary>
    public void Emit(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        ValidateConfiguration();

        count = Math.Min(count, MaxParticles - particles.Count);
        for (var index = 0; index < count; index++)
        {
            var lifetime = MathF.Max(float.Epsilon, Vary(Lifetime, LifetimeVariation));
            var speed = Vary(Speed, SpeedVariation);
            var angle = Direction + NextFloat(-Spread / 2f, Spread / 2f);
            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            var position = SimulationSpace == ParticleSimulationSpace.Local ? Vector2.Zero : Transform.Position;
            if (SimulationSpace == ParticleSimulationSpace.World)
                velocity = Vector2.TransformNormal(velocity, Matrix3x2.CreateRotation(Transform.Rotation));
            particles.Add(new Particle(position, velocity, lifetime, StartRotation));
        }
    }

    /// <summary>Resets the random sequence, useful for deterministic effects and tests.</summary>
    public void SetRandomSeed(int seed) => random = new Random(seed);

    public override void OnDestroy() => particles.Clear();

    private float Vary(float value, float variation) => value + NextFloat(-variation, variation);
    private float NextFloat(float minimum, float maximum) => minimum + (maximum - minimum) * random.NextSingle();

    private void ValidateConfiguration()
    {
        if (MaxParticles < 0)
            throw new InvalidOperationException("MaxParticles cannot be negative.");
        if (!float.IsFinite(EmissionRate) || EmissionRate < 0f)
            throw new InvalidOperationException("EmissionRate must be finite and non-negative.");
        if (!float.IsFinite(Lifetime) || !float.IsFinite(LifetimeVariation) || Lifetime <= 0f || LifetimeVariation < 0f)
            throw new InvalidOperationException("Particle lifetime values must be finite and Lifetime must be positive.");
        if (!float.IsFinite(StartSize) || !float.IsFinite(EndSize) || StartSize < 0f || EndSize < 0f)
            throw new InvalidOperationException("Particle sizes must be finite and non-negative.");
    }

    private static Color Lerp(Color start, Color end, float amount) => new(
        float.Lerp(start.R, end.R, amount),
        float.Lerp(start.G, end.G, amount),
        float.Lerp(start.B, end.B, amount),
        float.Lerp(start.A, end.A, amount));

    private struct Particle(Vector2 position, Vector2 velocity, float lifetime, float rotation)
    {
        public Vector2 Position = position;
        public Vector2 Velocity = velocity;
        public float Lifetime = lifetime;
        public float Rotation = rotation;
        public float Age;
    }
}

/// <summary>Controls whether existing particles follow the emitter transform.</summary>
public enum ParticleSimulationSpace
{
    World,
    Local
}
