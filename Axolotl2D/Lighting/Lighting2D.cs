using Axolotl2D.GameObjects;
using System.Numerics;

namespace Axolotl2D.Lighting;

public enum LightKind2D
{
    Point,
    Spot
}

/// <summary>A point or spot light evaluated by the built-in sprite shader.</summary>
public sealed class Light2D(GameObject gameObject, Lighting2D lighting) : Component(gameObject)
{
    public LightKind2D Kind { get; set; }
    public Color Color { get; set; } = Color.White;
    public float Intensity { get; set; } = 1f;
    public float Radius { get; set; } = 300f;
    public float Height { get; set; } = 80f;
    public float Falloff { get; set; } = 1f;
    public float SpotAngle { get; set; } = MathF.PI / 3f;
    public uint LayerMask { get; set; } = uint.MaxValue;
    public bool CastShadows { get; set; } = true;

    public override void OnEnable() => lighting.Add(this);
    public override void OnDisable() => lighting.Remove(this);
}

/// <summary>A closed local-space polygon that blocks shadow-casting lights.</summary>
public sealed class ShadowCaster2D(GameObject gameObject, Lighting2D lighting) : Component(gameObject)
{
    private Vector2[] polygon = [];

    public IReadOnlyList<Vector2> Polygon => polygon;
    public uint LayerMask { get; set; } = uint.MaxValue;

    public void SetPolygon(params Vector2[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 3) throw new ArgumentException("A shadow polygon requires at least three points.", nameof(points));
        polygon = points.ToArray();
    }

    public override void OnEnable() => lighting.Add(this);
    public override void OnDisable() => lighting.Remove(this);
}

/// <summary>Scene-scoped ambient light, dynamic lights, and polygon shadow casters.</summary>
public sealed class Lighting2D
{
    public const int MaximumLights = 16;
    public const int MaximumShadowEdges = 64;
    private readonly List<Light2D> lights = [];
    private readonly List<ShadowCaster2D> casters = [];
    private readonly List<LightData> lightSnapshot = new(MaximumLights);
    private readonly List<ShadowEdgeData> edgeSnapshot = new(MaximumShadowEdges);

    public Color AmbientColor { get; set; } = Color.White;
    public float AmbientIntensity { get; set; } = 1f;
    public bool Enabled { get; set; } = true;
    public IReadOnlyList<Light2D> Lights => lights;
    public IReadOnlyList<ShadowCaster2D> ShadowCasters => casters;

    internal void Add(Light2D light) { if (!lights.Contains(light)) lights.Add(light); }
    internal void Remove(Light2D light) => lights.Remove(light);
    internal void Add(ShadowCaster2D caster) { if (!casters.Contains(caster)) casters.Add(caster); }
    internal void Remove(ShadowCaster2D caster) => casters.Remove(caster);

    internal LightingSnapshot Snapshot()
    {
        if (!Enabled)
            return new(false, Vector3.One, [], []);

        var ambient = new Vector3(AmbientColor.R, AmbientColor.G, AmbientColor.B) * Math.Max(0f, AmbientIntensity);
        lightSnapshot.Clear();
        for (var index = 0; index < lights.Count && index < MaximumLights; index++)
        {
            var light = lights[index];
            lightSnapshot.Add(new(
                light.Transform.Position,
                Vector2.Normalize(light.Transform.Right),
                new Vector3(light.Color.R, light.Color.G, light.Color.B),
                Math.Max(0f, light.Intensity),
                Math.Max(0.001f, light.Radius),
                Math.Max(0.001f, light.Height),
                Math.Max(0.001f, light.Falloff),
                light.Kind,
                Math.Clamp(light.SpotAngle, 0.001f, MathF.Tau),
                light.LayerMask,
                light.CastShadows));
        }

        edgeSnapshot.Clear();
        foreach (var caster in casters)
        {
            if (caster.Polygon.Count < 3) continue;
            for (var index = 0; index < caster.Polygon.Count && edgeSnapshot.Count < MaximumShadowEdges; index++)
            {
                var start = caster.Transform.TransformPoint(caster.Polygon[index]);
                var end = caster.Transform.TransformPoint(caster.Polygon[(index + 1) % caster.Polygon.Count]);
                edgeSnapshot.Add(new(start, end, caster.LayerMask));
            }
            if (edgeSnapshot.Count == MaximumShadowEdges) break;
        }
        return new(true, ambient, lightSnapshot, edgeSnapshot);
    }
}

internal readonly record struct LightingSnapshot(
    bool Enabled,
    Vector3 Ambient,
    IReadOnlyList<LightData> Lights,
    IReadOnlyList<ShadowEdgeData> ShadowEdges);

internal readonly record struct LightData(
    Vector2 Position,
    Vector2 Direction,
    Vector3 Color,
    float Intensity,
    float Radius,
    float Height,
    float Falloff,
    LightKind2D Kind,
    float SpotAngle,
    uint LayerMask,
    bool CastShadows);

internal readonly record struct ShadowEdgeData(Vector2 Start, Vector2 End, uint LayerMask);
