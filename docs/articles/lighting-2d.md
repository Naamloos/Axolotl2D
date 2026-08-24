# 2D Lighting

Axolotl2D's built-in sprite shader combines ambient light, point or spot lights, tangent-space normal maps, and hard shadows from polygon casters. Lighting affects world-space sprite commands. Screen-space UI and custom shader batches keep their submitted colors.

## Configure ambient light

Inject the scene-scoped `Lighting2D` service:

```csharp
public sealed class CaveScene(Lighting2D lighting) : BaseScene
{
    public override void Load()
    {
        lighting.AmbientColor = Color.FromHTML("#26304A");
        lighting.AmbientIntensity = 0.25f;
    }
}
```

The default ambient value is white, so existing scenes retain their unlit appearance until you change it.

## Add lights

`Light2D` reads its position and spot direction from its GameObject transform:

```csharp
var torch = Instantiate("Torch");
torch.Transform.LocalPosition = new Vector2(320, 180);

var light = torch.AddComponent<Light2D>();
light.Kind = LightKind2D.Point;
light.Color = Color.FromHTML("#FFB45C");
light.Intensity = 1.8f;
light.Radius = 420f;
light.Height = 70f;
light.Falloff = 1.4f;
```

Set `Kind` to `Spot`, rotate the GameObject, and configure `SpotAngle` for a cone. `Height` controls the virtual Z distance used for normal-map diffuse lighting. `Radius` and `Falloff` control range and attenuation.

## Assign normal maps

Assign a tangent-space normal texture to the sprite:

```csharp
var sprite = new Sprite(colorTexture)
{
    NormalMap = normalTexture
};

gameObject.AddComponent<SpriteRenderer>().Sprite = sprite;
```

The color and normal textures must use matching dimensions and atlas regions. A missing normal map uses a flat `(0, 0, 1)` normal. Rotating or scaling the GameObject rotates the tangent basis used by the shader.

## Cast polygon shadows

Define a closed polygon in GameObject-local coordinates:

```csharp
var wall = Instantiate("Stone wall");
wall.Transform.LocalPosition = new Vector2(500, 240);
wall.AddComponent<ShadowCaster2D>().SetPolygon(
    new(-80, -20),
    new(80, -20),
    new(80, 20),
    new(-80, 20));
```

The framework transforms each edge into world space. For each fragment and shadow-casting light, the shader tests the segment between the light and fragment against those edges. This produces hard polygon shadows without a lightmap render target.

## Use lighting layers

`SpriteRenderer.LightingLayer`, `Light2D.LayerMask`, and `ShadowCaster2D.LayerMask` use bit masks:

```csharp
renderer.LightingLayer = 1u << 2;
light.LayerMask = 1u << 2;
caster.LayerMask = 1u << 2;
```

The built-in renderer processes up to 16 enabled lights and 64 transformed shadow edges per scene frame. It ignores later entries. Split large scenes into layers or keep inactive lights disabled when you reach those limits.

Custom `ShaderProgram` batches bypass built-in lighting. A custom shader owns its normal, light, and shadow inputs.
