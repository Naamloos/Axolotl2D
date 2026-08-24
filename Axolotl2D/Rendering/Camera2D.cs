using Axolotl2D.GameObjects;
using Axolotl2D.UI;
using Axolotl2D.Shaders;
using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>A normalized top-left viewport inside the game window.</summary>
public readonly record struct CameraViewport(float X, float Y, float Width, float Height)
{
    public static CameraViewport Full => new(0f, 0f, 1f, 1f);

    internal void Validate()
    {
        if (X < 0f || Y < 0f || Width <= 0f || Height <= 0f || X + Width > 1f || Y + Height > 1f)
            throw new ArgumentOutOfRangeException(nameof(CameraViewport), "A camera viewport must fit inside normalized window bounds.");
    }
}

/// <summary>Axis-aligned world bounds for a camera center and visible area.</summary>
public readonly record struct CameraBounds(Vector2 Position, Vector2 Size)
{
    public Vector2 Min => Position;
    public Vector2 Max => Position + Size;
}

/// <summary>A 2D camera with follow, bounds, smoothing, shake, zoom, and viewport support.</summary>
public sealed class Camera2D
{
    private const float MinimumZoom = 0.01f;
    private readonly Game game;
    private readonly string name;
    private CameraViewport viewport = CameraViewport.Full;
    private Vector2 position;
    private Vector2 shakeOffset;
    private float zoom = 1f;
    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeRemaining;
    private float shakeFrequency;
    private float shakePhase;
    private readonly List<PostProcessEffect> postProcessEffects = [];

    internal Camera2D(Game game, string name)
    {
        this.game = game;
        this.name = name;
    }

    /// <summary>Creates an unmanaged camera. Prefer CameraManager.Create for rendered cameras.</summary>
    public Camera2D(Game game) : this(game, "Camera") { }

    public string Name => name;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public uint CullingMask { get; set; } = uint.MaxValue;
    public float Rotation { get; set; }
    public Transform? FollowTarget { get; set; }
    public Vector2 FollowOffset { get; set; }
    public float FollowSmoothing { get; set; }
    public Vector2 DeadZone { get; set; }
    public CameraBounds? Bounds { get; set; }
    /// <summary>Optional off-screen destination. A targeted camera does not draw directly to the window.</summary>
    public RenderTexture? RenderTarget { get; set; }
    public IReadOnlyList<PostProcessEffect> PostProcessEffects => postProcessEffects;

    public Vector2 Position
    {
        get => position;
        set => position = ClampToBounds(value);
    }

    public float Zoom
    {
        get => zoom;
        set
        {
            zoom = Math.Max(MinimumZoom, value);
            position = ClampToBounds(position);
        }
    }

    public CameraViewport Viewport
    {
        get => viewport;
        set
        {
            value.Validate();
            viewport = value;
            position = ClampToBounds(position);
        }
    }

    public Vector2 ViewportSize => PixelViewport.Size;

    /// <summary>The camera viewport in top-left window pixels.</summary>
    public UIRect PixelViewport => new(
        new Vector2(game.Viewport.X * viewport.X, game.Viewport.Y * viewport.Y),
        new Vector2(game.Viewport.X * viewport.Width, game.Viewport.Y * viewport.Height));

    public void Pan(Vector2 worldDelta) => Position += worldDelta;

    public void Shake(float amplitude, float duration, float frequency = 24f)
    {
        if (amplitude < 0f || duration < 0f || frequency <= 0f)
            throw new ArgumentOutOfRangeException(nameof(amplitude));
        shakeAmplitude = amplitude;
        shakeDuration = duration;
        shakeRemaining = duration;
        shakeFrequency = frequency;
        shakePhase = 0f;
    }

    public void ZoomAt(float factor, Vector2 screenPosition)
    {
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
        var anchoredWorldPosition = ScreenToWorld(screenPosition);
        Zoom *= factor;
        Position += anchoredWorldPosition - ScreenToWorld(screenPosition);
    }

    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        var relative = worldPosition - (position + shakeOffset);
        var rotated = Vector2.Transform(relative, Matrix3x2.CreateRotation(-Rotation));
        return PixelViewport.Position + rotated * Zoom + ViewportSize / 2f;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        var relative = (screenPosition - PixelViewport.Position - ViewportSize / 2f) / Zoom;
        return Vector2.Transform(relative, Matrix3x2.CreateRotation(Rotation)) + position + shakeOffset;
    }

    internal void Update(double deltaTime)
    {
        if (FollowTarget is not null)
        {
            var target = FollowTarget.Position + FollowOffset;
            var delta = target - position;
            if (DeadZone.X > 0f) delta.X = MathF.CopySign(MathF.Max(0f, MathF.Abs(delta.X) - DeadZone.X / 2f), delta.X);
            if (DeadZone.Y > 0f) delta.Y = MathF.CopySign(MathF.Max(0f, MathF.Abs(delta.Y) - DeadZone.Y / 2f), delta.Y);
            var factor = FollowSmoothing <= 0f ? 1f : 1f - MathF.Exp(-FollowSmoothing * (float)deltaTime);
            Position += delta * factor;
        }

        shakeOffset = Vector2.Zero;
        if (shakeRemaining <= 0f) return;
        shakeRemaining = Math.Max(0f, shakeRemaining - (float)deltaTime);
        shakePhase += shakeFrequency * (float)deltaTime;
        var strength = shakeDuration <= 0f ? 0f : shakeAmplitude * shakeRemaining / shakeDuration;
        shakeOffset = new Vector2(MathF.Sin(shakePhase * 2.17f), MathF.Cos(shakePhase * 1.73f)) * strength;
    }

    internal PostProcessEffect AddPostProcess(ShaderProgram shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        PostProcessEffect? effect = null;
        effect = new(this, shader, () => postProcessEffects.Remove(effect!));
        postProcessEffects.Add(effect);
        return effect;
    }

    private Vector2 ClampToBounds(Vector2 value)
    {
        if (Bounds is not { } bounds) return value;
        var half = ViewportSize / (2f * zoom);
        var min = bounds.Min + half;
        var max = bounds.Max - half;
        value.X = min.X > max.X ? (bounds.Min.X + bounds.Max.X) / 2f : Math.Clamp(value.X, min.X, max.X);
        value.Y = min.Y > max.Y ? (bounds.Min.Y + bounds.Max.Y) / 2f : Math.Clamp(value.Y, min.Y, max.Y);
        return value;
    }
}

/// <summary>Owns the default camera and additional split-screen or inset cameras.</summary>
public sealed class CameraManager
{
    private readonly List<Camera2D> cameras = [];
    private readonly Game game;

    public CameraManager(Game game)
    {
        this.game = game;
        Default = new Camera2D(game, "Default");
        cameras.Add(Default);
    }

    public Camera2D Default { get; }
    public IReadOnlyList<Camera2D> Cameras => cameras;
    internal IEnumerable<Camera2D> ActiveCameras => cameras.Where(camera => camera.Enabled).OrderBy(camera => camera.Priority);

    public Camera2D Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (cameras.Any(camera => camera.Name == name))
            throw new InvalidOperationException($"A camera named '{name}' already exists.");
        var camera = new Camera2D(game, name);
        cameras.Add(camera);
        return camera;
    }

    public bool Remove(Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return !ReferenceEquals(camera, Default) && cameras.Remove(camera);
    }

    internal void Update(double deltaTime)
    {
        foreach (var camera in cameras) camera.Update(deltaTime);
    }
}
