# Data Prefabs

An `.axprefab` is a versioned JSON asset that describes a hierarchy of GameObjects, transforms, tags, and components. Prefabs contain no executable code. Component IDs resolve through explicit registrations, and scene dependency injection still constructs every component.

Use prefabs for repeatable object composition while keeping behavior in components. Prefab instantiation does not replace scenes, save games, or package GameObject factories.

## Create a prefab

```json
{
  "formatVersion": 1,
  "root": {
    "id": "crate",
    "name": "Crate",
    "active": true,
    "tags": ["destructible"],
    "transform": {
      "position": { "x": 0, "y": 0 },
      "rotation": 0,
      "scale": { "x": 1, "y": 1 }
    },
    "components": [
      {
        "type": "axolotl.sprite-renderer",
        "enabled": true,
        "data": {
          "sprite": { "texture": "crate" }
        }
      },
      {
        "type": "axolotl.physics-body",
        "data": {
          "bodyType": "dynamic",
          "shapes": [
            {
              "type": "box",
              "size": { "x": 64, "y": 64 },
              "density": 1,
              "friction": 0.6,
              "restitution": 0.1
            }
          ]
        }
      }
    ],
    "children": []
  }
}
```

All angles are radians. Positions, sizes, and physics dimensions use the same pixel-based world units as the runtime APIs. Component `data` must be a JSON object.

The root and each child support:

| Property | Default | Meaning |
| --- | --- | --- |
| `id` | None | Optional prefab-local ID used by component references |
| `name` | `GameObject` | Runtime GameObject name |
| `active` | `true` | Initial GameObject activation |
| `tags` | `[]` | Unique, case-sensitive tags |
| `transform.position` | `{ "x": 0, "y": 0 }` | Local position |
| `transform.rotation` | `0` | Local rotation |
| `transform.scale` | `{ "x": 1, "y": 1 }` | Local scale |
| `components` | `[]` | Components in attachment order |
| `children` | `[]` | Child GameObjects |

IDs must be unique inside one prefab. The loader rejects unknown structural properties, duplicate IDs and tags, non-finite transform values, excessive nesting, and unreasonably large object or component counts.

## Load and instantiate

Load standalone files during game initialization:

```csharp
await assets.LoadFileAsync<PrefabAsset>(
    "crate", "Assets/Prefabs/crate.axprefab", cancellationToken);
```

Instantiate from an active scene:

```csharp
var crate = Instantiate(assets.Get<PrefabAsset>("crate"));
crate.Transform.LocalPosition = new Vector2(300, 120);
```

The optional name argument overrides only the root name:

```csharp
var bossCrate = Instantiate(prefab, "Boss crate");
```

Instantiation creates the complete GameObject hierarchy first. It then attaches components in declaration order and resolves deferred object references. If any component ID, data, or reference is invalid, every object created by that call is scheduled for destruction and the exception is propagated.

Asset references such as `texture` and `font` are keys in `AssetManager`. Preload those assets before instantiating the prefab. This keeps prefab instantiation synchronous and consistent with normal scene construction.

## Package prefabs in `.axpkg`

Declare prefabs like any other module asset:

```xml
<AxolotlAsset Include="Assets\Prefabs\crate.axprefab"
               Name="prefabs/crate" />
```

The built-in MSBuild importer validates the prefab and records it as `PrefabAsset` with content type `application/vnd.axolotl2d.prefab+json`. Load it after mounting the package:

```csharp
await assets.LoadPackageAsync<PrefabAsset>(
    "crate", "my.game.content", "prefabs/crate", cancellationToken);
```

Content-only packages may contain prefabs. They can use any component IDs registered by the host game. A trusted executable module may additionally register its own prefab components.

## Custom components

A component can own its data contract by implementing `IPrefabDataReceiver`:

```csharp
public sealed record HealthPrefabData(int Maximum, int Current);

public sealed class Health(GameObject gameObject)
    : Component(gameObject), IPrefabDataReceiver
{
    public int Maximum { get; private set; }
    public int Current { get; private set; }

    public void LoadPrefabData(JsonElement data, PrefabLoadContext context)
    {
        var values = context.Deserialize<HealthPrefabData>(data);
        Maximum = values.Maximum;
        Current = values.Current;
    }
}
```

Register its stable ID while configuring the game:

```csharp
services.UseSceneManagerGameHost<MyGame>();
services.AddPrefabComponent<Health>("mygame.health");
```

Then use it in data:

```json
{
  "type": "mygame.health",
  "data": { "maximum": 100, "current": 75 }
}
```

`PrefabLoadContext.Deserialize<T>()` uses camel-case JSON, string enums, and rejects unknown properties. It also exposes `GetAsset<T>()`, `GetObject(id)`, and `GetComponent<T>(objectId)`.

Use `Defer()` when data refers to a component that might appear later in the prefab:

```csharp
public void LoadPrefabData(JsonElement data, PrefabLoadContext context)
{
    var values = context.Deserialize<FollowPrefabData>(data);
    context.Defer(() => Target = context.GetObject(values.Target).Transform);
}
```

Components receive prefab data before `Awake` and the initial `OnEnable`. A component may therefore validate required prefab values in `Awake`. Components required by `Awake` must appear earlier on the same GameObject. `Start` still runs after the entire prefab has been instantiated.

For a component that should not implement the interface, register a typed loader:

```csharp
services.AddPrefabComponent<Health, HealthPrefabData>(
    "mygame.health",
    static (health, values, _) => health.Set(values.Maximum, values.Current));
```

A trusted module registers an interface-based component from `IAxolotlModule.Initialize`:

```csharp
context.RegisterPrefabComponent<ModuleEnemy>("my.module.enemy");
```

Package component IDs are removed when their package is unloaded. Prefix custom IDs with the game or package ID to avoid collisions. Prefab JSON never names CLR types and never enables code from a content-only package.

## Built-in component IDs

Enums use camel-case names. Colors accept `#RRGGBB` and the built-in names `transparent`, `white`, `black`, `red`, `green`, `blue`, `yellow`, `cyan`, `magenta`, `gray`, `darkgray`, `lightgray`, `orange`, and `brown`.

| ID | Component | Main data properties |
| --- | --- | --- |
| `axolotl.sprite-renderer` | `SpriteRenderer` | `sprite`, `tint`, `space`, `depth`, `lightingLayer` |
| `axolotl.sprite-animator` | `SpriteAnimator` | `texture`, `frameWidth`, `frameHeight`, `margin`, `spacing`, `animations`, `play` |
| `axolotl.physics-body` | `PhysicsBody` | `bodyType`, damping, gravity, bullet flag, `shapes` |
| `axolotl.box-collider` | `BoxCollider` | `size`, `offset`, material, sensor flag, category/mask/group filter |
| `axolotl.circle-collider` | `CircleCollider` | `radius`, `offset`, material, sensor flag, category/mask/group filter |
| `axolotl.capsule-collider` | `CapsuleCollider` | `point1`, `point2`, `radius`, material, sensor flag, category/mask/group filter |
| `axolotl.polygon-collider` | `PolygonCollider` | `points`, material, sensor flag, category/mask/group filter |
| `axolotl.segment-collider` | `SegmentCollider` | `point1`, `point2`, material, sensor flag, category/mask/group filter |
| `axolotl.distance-joint` | `DistanceJoint` | connected body ID, local anchors, length, spring, limits, motor |
| `axolotl.revolute-joint` | `RevoluteJoint` | connected body ID, local anchors, spring, angular limits, motor |
| `axolotl.light` | `Light2D` | kind, color, intensity, radius, height, falloff, spot angle, layers, shadows |
| `axolotl.shadow-caster` | `ShadowCaster2D` | `points`, `layerMask` |
| `axolotl.particle-emitter` | `ParticleEmitter` | The public emitter settings, optional `sprite`, and `randomSeed` |
| `axolotl.ui-transform` | `UITransform` | Anchors, pivot, position, size, offsets, size limits, optional parent object ID |
| `axolotl.ui-visual` | `UIVisual` | `sprite`, primitive, color, thickness, depth |
| `axolotl.ui-text` | `UIText` | font key, text, font size, color, alignment, depth |
| `axolotl.ui-layout` | `UILayoutGroup` | direction, alignment, padding, spacing, child expansion |
| `axolotl.ui-clip` | `UIClip` | No data |
| `axolotl.ui-button` | `UIButton` | pointer button, interactable, navigation order, depth |
| `axolotl.ui-toggle` | `UIToggle` | Selectable settings and initial value |
| `axolotl.ui-slider` | `UISlider` | Selectable settings, minimum, maximum, step, value |
| `axolotl.ui-progress-bar` | `UIProgressBar` | value, background color, fill color, depth |
| `axolotl.ui-scroll-view` | `UIScrollView` | content object ID, content size, wheel speed, enabled axes |

### Sprite data

Components that accept `sprite` use this shared shape:

```json
{
  "texture": "logo",
  "normalMap": "logo-normal",
  "source": { "x": 0, "y": 0, "width": 64, "height": 64 },
  "origin": { "x": 0.5, "y": 0.5 }
}
```

Only `texture` is required.

### Physics shapes

`bodyType` is `static`, `kinematic`, or `dynamic`. Shapes are `box`, `circle`, `capsule`, `polygon`, or `segment`:

```json
"shapes": [
  { "type": "box", "size": { "x": 80, "y": 40 } },
  { "type": "circle", "radius": 20, "restitution": 0.5 },
  { "type": "capsule", "point1": { "x": 0, "y": -20 }, "point2": { "x": 0, "y": 20 }, "radius": 12 },
  { "type": "polygon", "points": [{ "x": -20, "y": 20 }, { "x": 0, "y": -20 }, { "x": 20, "y": 20 }] },
  { "type": "segment", "point1": { "x": -50, "y": 0 }, "point2": { "x": 50, "y": 0 } }
]
```

Each shape also accepts `density`, `friction`, and `restitution` with the same defaults and validation as `PhysicsBody`.

`shapes` is optional when the GameObject declares a separate collider component. Collider components add sensor behavior, 64-bit `categoryBits` and `maskBits`, and `groupIndex` while keeping the inline shape format compatible. Polygons accept three to eight finite points and are reduced to a valid convex hull.

Joint `connectedBody` values refer to a prefab object ID. `anchorA` is local to the joint's GameObject and `anchorB` is local to the connected body's GameObject. The loader resolves the body reference after the complete prefab hierarchy exists, so moving or rotating the instantiated prefab before `Start` preserves the joint geometry:

```json
{
  "type": "axolotl.distance-joint",
  "data": {
    "connectedBody": "anchor",
    "anchorA": { "x": 0, "y": 0 },
    "anchorB": { "x": 0, "y": 0 },
    "length": 180,
    "maximumLength": 180,
    "enableSpring": true
  }
}
```

### Sprite animations

The animator slices one texture into a uniform sheet. Omitting `frames` uses every frame in that sheet:

```json
{
  "texture": "run",
  "frameWidth": 255,
  "frameHeight": 255,
  "animations": [
    {
      "name": "run",
      "framesPerSecond": 20,
      "playback": "loop",
      "frames": [
        { "index": 0 },
        { "index": 1, "duration": 0.08, "marker": "footstep" },
        { "index": 2 }
      ]
    }
  ],
  "play": "run"
}
```

The animator requires an `axolotl.sprite-renderer` on the same GameObject.
