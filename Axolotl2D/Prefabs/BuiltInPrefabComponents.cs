using Axolotl2D.Animation;
using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Lighting;
using Axolotl2D.Particles;
using Axolotl2D.Physics;
using Axolotl2D.Rendering;
using Axolotl2D.UI;
using Box2D.NET;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Prefabs;

/// <summary>Stable IDs understood by the built-in prefab component loaders.</summary>
public static class PrefabComponentIds
{
    public const string SpriteRenderer = "axolotl.sprite-renderer";
    public const string SpriteAnimator = "axolotl.sprite-animator";
    public const string PhysicsBody = "axolotl.physics-body";
    public const string BoxCollider = "axolotl.box-collider";
    public const string CircleCollider = "axolotl.circle-collider";
    public const string DistanceJoint = "axolotl.distance-joint";
    public const string RevoluteJoint = "axolotl.revolute-joint";
    public const string Light = "axolotl.light";
    public const string ShadowCaster = "axolotl.shadow-caster";
    public const string ParticleEmitter = "axolotl.particle-emitter";
    public const string UITransform = "axolotl.ui-transform";
    public const string UIVisual = "axolotl.ui-visual";
    public const string UIText = "axolotl.ui-text";
    public const string UILayout = "axolotl.ui-layout";
    public const string UIClip = "axolotl.ui-clip";
    public const string UIButton = "axolotl.ui-button";
    public const string UIToggle = "axolotl.ui-toggle";
    public const string UISlider = "axolotl.ui-slider";
    public const string UIProgressBar = "axolotl.ui-progress-bar";
    public const string UIScrollView = "axolotl.ui-scroll-view";
}

internal static class BuiltInPrefabComponents
{
    public static IReadOnlyList<PrefabComponentRegistration> Registrations { get; } =
    [
        PrefabComponentRegistration.Create<SpriteRenderer, SpriteRendererData>(PrefabComponentIds.SpriteRenderer, Load),
        PrefabComponentRegistration.Create<SpriteAnimator, SpriteAnimatorData>(PrefabComponentIds.SpriteAnimator, Load),
        PrefabComponentRegistration.Create<PhysicsBody, PhysicsBodyData>(PrefabComponentIds.PhysicsBody, Load),
        PrefabComponentRegistration.Create<BoxCollider, BoxColliderData>(PrefabComponentIds.BoxCollider, Load),
        PrefabComponentRegistration.Create<CircleCollider, CircleColliderData>(PrefabComponentIds.CircleCollider, Load),
        PrefabComponentRegistration.Create<DistanceJoint, DistanceJointData>(PrefabComponentIds.DistanceJoint, Load),
        PrefabComponentRegistration.Create<RevoluteJoint, RevoluteJointData>(PrefabComponentIds.RevoluteJoint, Load),
        PrefabComponentRegistration.Create<Light2D, LightData>(PrefabComponentIds.Light, Load),
        PrefabComponentRegistration.Create<ShadowCaster2D, ShadowCasterData>(PrefabComponentIds.ShadowCaster, Load),
        PrefabComponentRegistration.Create<ParticleEmitter, ParticleEmitterData>(PrefabComponentIds.ParticleEmitter, Load),
        PrefabComponentRegistration.Create<UITransform, UITransformData>(PrefabComponentIds.UITransform, Load),
        PrefabComponentRegistration.Create<UIVisual, UIVisualData>(PrefabComponentIds.UIVisual, Load),
        PrefabComponentRegistration.Create<UIText, UITextData>(PrefabComponentIds.UIText, Load),
        PrefabComponentRegistration.Create<UILayoutGroup, UILayoutData>(PrefabComponentIds.UILayout, Load),
        PrefabComponentRegistration.Create<UIClip, EmptyData>(PrefabComponentIds.UIClip, static (_, _, _) => { }),
        PrefabComponentRegistration.Create<UIButton, UISelectableData>(PrefabComponentIds.UIButton, Load),
        PrefabComponentRegistration.Create<UIToggle, UIToggleData>(PrefabComponentIds.UIToggle, Load),
        PrefabComponentRegistration.Create<UISlider, UISliderData>(PrefabComponentIds.UISlider, Load),
        PrefabComponentRegistration.Create<UIProgressBar, UIProgressBarData>(PrefabComponentIds.UIProgressBar, Load),
        PrefabComponentRegistration.Create<UIScrollView, UIScrollViewData>(PrefabComponentIds.UIScrollView, Load)
    ];

    private static void Load(SpriteRenderer component, SpriteRendererData data, PrefabLoadContext context)
    {
        if (data.Sprite is null) throw new InvalidDataException("Sprite renderer prefab data requires a sprite.");
        component.Sprite = CreateSprite(data.Sprite, context);
        if (data.Tint is not null) component.Tint = ParseColor(data.Tint);
        if (data.Space is not null) component.Space = data.Space.Value;
        if (data.Depth is not null) component.Depth = data.Depth.Value;
        if (data.LightingLayer is not null) component.LightingLayer = data.LightingLayer.Value;
    }

    private static void Load(SpriteAnimator component, SpriteAnimatorData data, PrefabLoadContext context)
    {
        if (string.IsNullOrWhiteSpace(data.Texture) || data.FrameWidth <= 0 || data.FrameHeight <= 0 || data.Animations is null)
            throw new InvalidDataException("Sprite animator prefab data requires a texture and positive frame dimensions.");
        var sheet = new SpriteSheet(context.GetAsset<Texture2D>(data.Texture), data.FrameWidth, data.FrameHeight,
            data.Margin, data.Spacing);
        foreach (var animation in data.Animations)
        {
            if (string.IsNullOrWhiteSpace(animation.Name))
                throw new InvalidDataException("Sprite animation names cannot be empty.");
            component.Add(animation.Name, new SpriteAnimation(sheet.Sprites, animation.FramesPerSecond, animation.Loop));
        }
        if (data.Play is not null) component.Play(data.Play);
    }

    private static void Load(PhysicsBody component, PhysicsBodyData data, PrefabLoadContext context)
    {
        component.Type = data.BodyType switch
        {
            PrefabPhysicsBodyType.Static => B2BodyType.b2_staticBody,
            PrefabPhysicsBodyType.Kinematic => B2BodyType.b2_kinematicBody,
            PrefabPhysicsBodyType.Dynamic => B2BodyType.b2_dynamicBody,
            _ => throw new ArgumentOutOfRangeException(nameof(data.BodyType))
        };
        component.LinearDamping = data.LinearDamping;
        component.AngularDamping = data.AngularDamping;
        component.GravityScale = data.GravityScale;
        component.IsBullet = data.IsBullet;
        foreach (var shape in data.Shapes ?? [])
            switch (shape.Type)
            {
                case PrefabPhysicsShapeType.Box:
                    component.AddBox(ReadVector(shape.Size, "box size"), shape.Density, shape.Friction, shape.Restitution);
                    break;
                case PrefabPhysicsShapeType.Circle:
                    if (shape.Radius is null) throw new InvalidDataException("Circle shapes require a radius.");
                    component.AddCircle(shape.Radius.Value, shape.Density, shape.Friction, shape.Restitution);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(shape.Type));
            }
    }

    private static void Load(BoxCollider component, BoxColliderData data, PrefabLoadContext context)
    {
        component.Size = ReadVector(data.Size, "box collider size");
        component.Offset = ReadVector(data.Offset ?? new(0f, 0f), "box collider offset");
        LoadCollider(component, data);
    }

    private static void Load(CircleCollider component, CircleColliderData data, PrefabLoadContext context)
    {
        component.Radius = data.Radius;
        component.Offset = ReadVector(data.Offset ?? new(0f, 0f), "circle collider offset");
        LoadCollider(component, data);
    }

    private static void LoadCollider(PhysicsCollider component, ColliderData data)
    {
        component.Density = data.Density;
        component.Friction = data.Friction;
        component.Restitution = data.Restitution;
        component.IsSensor = data.IsSensor;
        component.CategoryBits = data.CategoryBits;
        component.MaskBits = data.MaskBits;
        component.GroupIndex = data.GroupIndex;
    }

    private static void Load(DistanceJoint component, DistanceJointData data, PrefabLoadContext context)
    {
        LoadJoint(component, data, context);
        component.LocalAnchorA = ReadVector(data.AnchorA, "distance joint anchor A");
        component.LocalAnchorB = ReadVector(data.AnchorB, "distance joint anchor B");
        component.Length = data.Length;
        component.EnableSpring = data.EnableSpring;
        component.Hertz = data.Hertz;
        component.DampingRatio = data.DampingRatio;
        component.EnableLimit = data.EnableLimit;
        component.MinimumLength = data.MinimumLength;
        component.MaximumLength = data.MaximumLength;
        component.EnableMotor = data.EnableMotor;
        component.MaximumMotorForce = data.MaximumMotorForce;
        component.MotorSpeed = data.MotorSpeed;
    }

    private static void Load(RevoluteJoint component, RevoluteJointData data, PrefabLoadContext context)
    {
        LoadJoint(component, data, context);
        component.LocalAnchorA = ReadVector(data.AnchorA, "revolute joint anchor A");
        component.LocalAnchorB = ReadVector(data.AnchorB, "revolute joint anchor B");
        component.EnableSpring = data.EnableSpring;
        component.Hertz = data.Hertz;
        component.DampingRatio = data.DampingRatio;
        component.EnableLimit = data.EnableLimit;
        component.LowerAngle = data.LowerAngle;
        component.UpperAngle = data.UpperAngle;
        component.EnableMotor = data.EnableMotor;
        component.MaximumMotorTorque = data.MaximumMotorTorque;
        component.MotorSpeed = data.MotorSpeed;
    }

    private static void LoadJoint(PhysicsJoint component, JointData data, PrefabLoadContext context)
    {
        if (string.IsNullOrWhiteSpace(data.ConnectedBody))
            throw new InvalidDataException("Physics joint prefab data requires a connected body object ID.");
        component.CollideConnected = data.CollideConnected;
        context.Defer(() =>
        {
            var connectedBody = context.GetComponent<PhysicsBody>(data.ConnectedBody);
            component.ConnectedBody = connectedBody;
        });
    }

    private static void Load(Light2D component, LightData data, PrefabLoadContext context)
    {
        component.Kind = data.Kind;
        if (data.Color is not null) component.Color = ParseColor(data.Color);
        component.Intensity = data.Intensity;
        component.Radius = data.Radius;
        component.Height = data.Height;
        component.Falloff = data.Falloff;
        component.SpotAngle = data.SpotAngle;
        component.LayerMask = data.LayerMask;
        component.CastShadows = data.CastShadows;
    }

    private static void Load(ShadowCaster2D component, ShadowCasterData data, PrefabLoadContext context)
    {
        if (data.Points is null) throw new InvalidDataException("Shadow caster prefab data requires points.");
        component.LayerMask = data.LayerMask;
        component.SetPolygon(data.Points.Select(point => ReadVector(point, "shadow point")).ToArray());
    }

    private static void Load(ParticleEmitter component, ParticleEmitterData data, PrefabLoadContext context)
    {
        if (data.Sprite is not null) component.Sprite = CreateSprite(data.Sprite, context);
        component.Space = data.Space;
        component.SimulationSpace = data.SimulationSpace;
        component.MaxParticles = data.MaxParticles;
        component.EmissionRate = data.EmissionRate;
        component.Lifetime = data.Lifetime;
        component.LifetimeVariation = data.LifetimeVariation;
        component.Speed = data.Speed;
        component.SpeedVariation = data.SpeedVariation;
        component.Direction = data.Direction;
        component.Spread = data.Spread;
        component.Acceleration = ReadVector(data.Acceleration ?? new(0f, 0f), "particle acceleration");
        component.StartSize = data.StartSize;
        component.EndSize = data.EndSize;
        component.StartColor = ParseColor(data.StartColor);
        component.EndColor = ParseColor(data.EndColor);
        component.StartRotation = data.StartRotation;
        component.AngularVelocity = data.AngularVelocity;
        component.Depth = data.Depth;
        component.PlayOnStart = data.PlayOnStart;
        if (data.RandomSeed is not null) component.SetRandomSeed(data.RandomSeed.Value);
        component.Emit(0);
    }

    private static void Load(UITransform component, UITransformData data, PrefabLoadContext context)
    {
        if (data.Anchor is not null) component.Anchor = ReadVector(data.Anchor, "UI anchor");
        if (data.AnchorMin is not null) component.AnchorMin = ReadVector(data.AnchorMin, "UI minimum anchor");
        if (data.AnchorMax is not null) component.AnchorMax = ReadVector(data.AnchorMax, "UI maximum anchor");
        component.Pivot = ReadVector(data.Pivot ?? new(0f, 0f), "UI pivot");
        component.AnchoredPosition = ReadVector(data.AnchoredPosition ?? new(0f, 0f), "UI anchored position");
        component.Size = ReadVector(data.Size ?? new(100f, 30f), "UI size");
        component.OffsetMin = ReadVector(data.OffsetMin ?? new(0f, 0f), "UI minimum offset");
        component.OffsetMax = ReadVector(data.OffsetMax ?? new(0f, 0f), "UI maximum offset");
        component.MinSize = ReadVector(data.MinSize ?? new(0f, 0f), "UI minimum size");
        component.MaxSize = data.MaxSize is null
            ? new Vector2(float.PositiveInfinity)
            : ReadVector(data.MaxSize, "UI maximum size");
        if (data.Parent is not null)
            context.Defer(() => component.SetParent(context.GetComponent<UITransform>(data.Parent), screenPositionStays: false));
    }

    private static void Load(UIVisual component, UIVisualData data, PrefabLoadContext context)
    {
        if (data.Sprite is not null) component.Sprite = CreateSprite(data.Sprite, context);
        component.Primitive = data.Primitive;
        component.Color = ParseColor(data.Color);
        component.Thickness = data.Thickness;
        component.Depth = data.Depth;
    }

    private static void Load(UIText component, UITextData data, PrefabLoadContext context)
    {
        if (string.IsNullOrWhiteSpace(data.Font)) throw new InvalidDataException("UI text prefab data requires a font asset key.");
        if (data.FontSize <= 0f) throw new InvalidDataException("UI text font size must be positive.");
        component.Font = context.GetAsset<FontAsset>(data.Font);
        component.Text = data.Text;
        component.FontSize = data.FontSize;
        component.Color = ParseColor(data.Color);
        component.HorizontalAlignment = data.HorizontalAlignment;
        component.VerticalAlignment = data.VerticalAlignment;
        component.Depth = data.Depth;
    }

    private static void Load(UILayoutGroup component, UILayoutData data, PrefabLoadContext context)
    {
        component.Direction = data.Direction;
        component.Alignment = data.Alignment;
        var padding = data.Padding ?? new Vector4Data(0f, 0f, 0f, 0f);
        component.Padding = new(padding.X, padding.Y, padding.Z, padding.W);
        component.Spacing = data.Spacing;
        component.ExpandChildren = data.ExpandChildren;
    }

    private static void Load(UIButton component, UISelectableData data, PrefabLoadContext context) =>
        LoadSelectable(component, data);

    private static void Load(UIToggle component, UIToggleData data, PrefabLoadContext context)
    {
        LoadSelectable(component, data);
        component.SetValue(data.Value, notify: false);
    }

    private static void Load(UISlider component, UISliderData data, PrefabLoadContext context)
    {
        LoadSelectable(component, data);
        component.Minimum = data.Minimum;
        component.Maximum = data.Maximum;
        component.Step = data.Step;
        component.SetValue(data.Value, notify: false);
    }

    private static void Load(UIProgressBar component, UIProgressBarData data, PrefabLoadContext context)
    {
        component.Value = data.Value;
        component.BackgroundColor = ParseColor(data.BackgroundColor);
        component.FillColor = ParseColor(data.FillColor);
        component.Depth = data.Depth;
    }

    private static void Load(UIScrollView component, UIScrollViewData data, PrefabLoadContext context)
    {
        if (string.IsNullOrWhiteSpace(data.Content)) throw new InvalidDataException("UI scroll view prefab data requires a content object ID.");
        if (data.ContentSize is null) throw new InvalidDataException("UI scroll view prefab data requires a content size.");
        component.ContentSize = ReadVector(data.ContentSize, "scroll content size");
        component.WheelSpeed = data.WheelSpeed;
        component.Horizontal = data.Horizontal;
        component.Vertical = data.Vertical;
        context.Defer(() => component.Content = context.GetComponent<UITransform>(data.Content));
    }

    private static void LoadSelectable(UISelectable component, UISelectableData data)
    {
        component.Button = data.Button;
        component.Interactable = data.Interactable;
        component.NavigationOrder = data.NavigationOrder;
        component.Depth = data.Depth;
    }

    private static Sprite CreateSprite(SpriteData data, PrefabLoadContext context)
    {
        if (string.IsNullOrWhiteSpace(data.Texture)) throw new InvalidDataException("Sprite prefab data requires a texture asset key.");
        var texture = context.GetAsset<Texture2D>(data.Texture);
        TextureRegion? source = data.Source is null
            ? null
            : new TextureRegion(data.Source.X, data.Source.Y, data.Source.Width, data.Source.Height);
        var sprite = new Sprite(texture, source);
        if (data.NormalMap is not null) sprite.NormalMap = context.GetAsset<Texture2D>(data.NormalMap);
        if (data.Origin is not null) sprite.Origin = ReadVector(data.Origin, "sprite origin");
        return sprite;
    }

    private static Vector2 ReadVector(VectorData? data, string name)
    {
        if (data is null || !float.IsFinite(data.X) || !float.IsFinite(data.Y))
            throw new InvalidDataException($"Prefab {name} requires finite x and y values.");
        return new(data.X, data.Y);
    }

    private static Color ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Prefab colors cannot be empty.");
        return value.ToLowerInvariant() switch
        {
            "transparent" => Color.Transparent,
            "white" => Color.White,
            "black" => Color.Black,
            "red" => Color.Red,
            "green" => Color.Green,
            "blue" => Color.Blue,
            "yellow" => Color.Yellow,
            "cyan" => Color.Cyan,
            "magenta" => Color.Magenta,
            "gray" => Color.Gray,
            "darkgray" => Color.DarkGray,
            "lightgray" => Color.LightGray,
            "orange" => Color.Orange,
            "brown" => Color.Brown,
            _ => Color.FromHTML(value)
        };
    }

    public sealed record EmptyData;
    public sealed record VectorData(float X, float Y);
    public sealed record Vector4Data(float X, float Y, float Z, float W);
    public sealed record RegionData(int X, int Y, int Width, int Height);
    public sealed record SpriteData(string Texture, string? NormalMap = null, RegionData? Source = null, VectorData? Origin = null);
    public sealed record SpriteRendererData(SpriteData? Sprite, string? Tint = null, CoordinateSpace? Space = null,
        float? Depth = null, uint? LightingLayer = null);
    public sealed record SpriteAnimatorData(string Texture, int FrameWidth, int FrameHeight,
        IReadOnlyList<SpriteAnimationData> Animations, int Margin = 0, int Spacing = 0, string? Play = null);
    public sealed record SpriteAnimationData(string Name, float FramesPerSecond, bool Loop = true);
    public enum PrefabPhysicsBodyType { Static, Kinematic, Dynamic }
    public enum PrefabPhysicsShapeType { Box, Circle }
    public sealed record PhysicsBodyData(PrefabPhysicsBodyType BodyType = PrefabPhysicsBodyType.Dynamic,
        float LinearDamping = 0f, float AngularDamping = 0f, float GravityScale = 1f, bool IsBullet = false,
        IReadOnlyList<PhysicsShapeData>? Shapes = null);
    public sealed record PhysicsShapeData(PrefabPhysicsShapeType Type, VectorData? Size = null, float? Radius = null,
        float Density = 1f, float Friction = 0.6f, float Restitution = 0f);
    public record ColliderData(float Density = 1f, float Friction = 0.6f, float Restitution = 0f,
        bool IsSensor = false, ulong CategoryBits = 1, ulong MaskBits = ulong.MaxValue, int GroupIndex = 0);
    public sealed record BoxColliderData(VectorData Size, VectorData? Offset = null, float Density = 1f,
        float Friction = 0.6f, float Restitution = 0f, bool IsSensor = false,
        ulong CategoryBits = 1, ulong MaskBits = ulong.MaxValue, int GroupIndex = 0)
        : ColliderData(Density, Friction, Restitution, IsSensor, CategoryBits, MaskBits, GroupIndex);
    public sealed record CircleColliderData(float Radius, VectorData? Offset = null, float Density = 1f,
        float Friction = 0.6f, float Restitution = 0f, bool IsSensor = false,
        ulong CategoryBits = 1, ulong MaskBits = ulong.MaxValue, int GroupIndex = 0)
        : ColliderData(Density, Friction, Restitution, IsSensor, CategoryBits, MaskBits, GroupIndex);
    public record JointData(string ConnectedBody, bool CollideConnected = false);
    public sealed record DistanceJointData(string ConnectedBody, VectorData AnchorA, VectorData AnchorB,
        float Length = 100f, bool CollideConnected = false, bool EnableSpring = false, float Hertz = 4f,
        float DampingRatio = 0.7f, bool EnableLimit = false, float MinimumLength = 0f,
        float MaximumLength = 100f, bool EnableMotor = false, float MaximumMotorForce = 0f,
        float MotorSpeed = 0f) : JointData(ConnectedBody, CollideConnected);
    public sealed record RevoluteJointData(string ConnectedBody, VectorData AnchorA, VectorData AnchorB,
        bool CollideConnected = false, bool EnableSpring = false, float Hertz = 4f,
        float DampingRatio = 0.7f, bool EnableLimit = false, float LowerAngle = 0f,
        float UpperAngle = 0f, bool EnableMotor = false, float MaximumMotorTorque = 0f,
        float MotorSpeed = 0f) : JointData(ConnectedBody, CollideConnected);
    public sealed record LightData(LightKind2D Kind = LightKind2D.Point, string? Color = null, float Intensity = 1f,
        float Radius = 300f, float Height = 80f, float Falloff = 1f, float SpotAngle = 1.0471976f,
        uint LayerMask = uint.MaxValue, bool CastShadows = true);
    public sealed record ShadowCasterData(IReadOnlyList<VectorData> Points, uint LayerMask = uint.MaxValue);
    public sealed record ParticleEmitterData(SpriteData? Sprite = null, CoordinateSpace Space = CoordinateSpace.World,
        ParticleSimulationSpace SimulationSpace = ParticleSimulationSpace.World, int MaxParticles = 1000,
        float EmissionRate = 10f, float Lifetime = 1f, float LifetimeVariation = 0f, float Speed = 100f,
        float SpeedVariation = 0f, float Direction = -1.5707964f, float Spread = 6.2831855f,
        VectorData? Acceleration = null, float StartSize = 8f, float EndSize = 0f, string StartColor = "white",
        string EndColor = "transparent", float StartRotation = 0f, float AngularVelocity = 0f, float Depth = 0f,
        bool PlayOnStart = true, int? RandomSeed = null);
    public sealed record UITransformData(VectorData? Anchor = null, VectorData? AnchorMin = null,
        VectorData? AnchorMax = null, VectorData? Pivot = null, VectorData? AnchoredPosition = null,
        VectorData? Size = null, VectorData? OffsetMin = null, VectorData? OffsetMax = null,
        VectorData? MinSize = null, VectorData? MaxSize = null, string? Parent = null);
    public sealed record UIVisualData(SpriteData? Sprite = null, UIPrimitive Primitive = UIPrimitive.Rectangle,
        string Color = "white", float Thickness = 1f, float Depth = 0f);
    public sealed record UITextData(string Font, string Text = "", float FontSize = 16f, string Color = "white",
        UIHorizontalAlignment HorizontalAlignment = UIHorizontalAlignment.Left,
        UIVerticalAlignment VerticalAlignment = UIVerticalAlignment.Top, float Depth = 0f);
    public sealed record UILayoutData(UILayoutDirection Direction = UILayoutDirection.Vertical,
        UILayoutAlignment Alignment = UILayoutAlignment.Start, Vector4Data? Padding = null,
        float Spacing = 0f, bool ExpandChildren = false);
    public record UISelectableData(MouseButton Button = MouseButton.Left, bool Interactable = true,
        int NavigationOrder = 0, float Depth = 0f);
    public sealed record UIToggleData(bool Value = false, MouseButton Button = MouseButton.Left, bool Interactable = true,
        int NavigationOrder = 0, float Depth = 0f) : UISelectableData(Button, Interactable, NavigationOrder, Depth);
    public sealed record UISliderData(float Minimum = 0f, float Maximum = 1f, float Step = 0.1f, float Value = 0f,
        MouseButton Button = MouseButton.Left, bool Interactable = true, int NavigationOrder = 0, float Depth = 0f)
        : UISelectableData(Button, Interactable, NavigationOrder, Depth);
    public sealed record UIProgressBarData(float Value = 0f, string BackgroundColor = "darkgray",
        string FillColor = "green", float Depth = 0f);
    public sealed record UIScrollViewData(string Content, VectorData ContentSize, float WheelSpeed = 36f,
        bool Horizontal = false, bool Vertical = true);
}
