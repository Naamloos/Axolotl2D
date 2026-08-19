# GameObjects and Components

Axolotl2D uses a composition model similar to Unity: a `GameObject` supplies identity, activation, and a `Transform`; components add behavior and rendering.

## Create an object

Instantiate objects from the active scene:

```csharp
var player = Instantiate("Player");
player.Transform.LocalPosition = new Vector2(100, 80);

var renderer = player.AddComponent<SpriteRenderer>();
renderer.Sprite = new Sprite(assets.Get<Texture2D>("player"));

player.AddComponent<PlayerController>();
```

The returned object belongs to the scene immediately. Its components receive dependencies from the current scene scope.

## Write a component

Every component constructor accepts its owning `GameObject`. Other parameters can be resolved by DI:

```csharp
public sealed class PlayerController(
    GameObject gameObject,
    MovementSettings settings) : Component(gameObject)
{
    public override void Update(double deltaTime)
    {
        var distance = settings.Speed * (float)deltaTime;
        Transform.Translate(Vector2.UnitX * distance);
    }
}
```

Use `GameObject`, `Transform`, `Enabled`, `IsActiveAndEnabled`, and `HasStarted` from the component base class. Components do not need to locate global state through static accessors.

## Find and remove components

```csharp
var renderer = player.GetComponent<SpriteRenderer>();

if (renderer is not null)
{
    renderer.Tint = Color.Red;
}

player.RemoveComponent<PlayerController>();
```

`GetComponent<T>()` returns the first matching component. Removing a component invokes its disable and destruction callbacks as applicable, then disposes it.

## Activation

`GameObject.Active` controls all components on the object. `Component.Enabled` controls one component:

```csharp
player.Active = false;
renderer.Enabled = false;
```

`IsActiveAndEnabled` is true only when both values allow the component to run. State changes trigger `OnEnable` or `OnDisable`; they do not repeat `Awake` or `Start`.

## Custom GameObject types

Use the generic overload when a domain-specific object type is useful:

```csharp
public sealed class EnemyObject(
    IServiceProvider services,
    string name,
    EnemyCatalog catalog) : GameObject(services, name)
{
    public EnemyCatalog Catalog { get; } = catalog;
}

var boss = Instantiate<EnemyObject>("Boss");
```

The factory passes the requested name and resolves the other constructor arguments from the scene scope. Prefer components for reusable behavior; custom GameObject subclasses are best kept for meaningful domain identity or construction rules.

See [Component Lifecycle](component-lifecycle.md), [Runtime GameObjects](runtime-gameobjects.md), and [Transforms and Hierarchies](transforms-and-hierarchies.md).
