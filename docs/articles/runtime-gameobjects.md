# Runtime GameObjects

Scenes can instantiate and destroy GameObjects during gameplay. The scene defers structural removal until the current lifecycle phase finishes, so a component can destroy itself without invalidating callback iteration.

## Instantiate at runtime

Call `Instantiate` during scene or component callbacks:

```csharp
public sealed class EnemySpawner(GameObject gameObject, EnemyAssets enemyAssets)
    : Component(gameObject)
{
    private double remaining;

    public override void Update(double deltaTime)
    {
        remaining -= deltaTime;
        if (remaining > 0)
        {
            return;
        }

        remaining = 2;
        var enemy = GameObject.Scene.Instantiate("Enemy");
        enemy.Transform.LocalPosition = Transform.Position;
        enemy.AddComponent<SpriteRenderer>().Sprite = enemyAssets.Sprite;
        enemy.AddComponent<EnemyController>();
    }
}
```

Configure newly added components before their first fixed update, update, or render. `Awake` and an initial `OnEnable` run when the component attaches; `Start` is deferred until the first active lifecycle pass.

Use [data prefabs](prefabs.md) when the same hierarchy and component configuration should be instantiated repeatedly from JSON:

```csharp
var enemy = Instantiate(assets.Get<PrefabAsset>("enemy"));
```

## Destroy safely

Destroy through either the object or scene:

```csharp
GameObject.Destroy();

// Equivalent when you already have a scene reference:
GameObject.Scene.Destroy(GameObject);
```

Destruction marks the object inactive immediately. No later component callbacks run for it in the current phase. The scene removes and disposes it after that phase.

`BaseScene.Destroy` returns `false` when the object does not belong to that scene or is already queued for destruction. Calling `GameObject.Destroy()` delegates to its owning scene.

## References to destroyed objects

Use `GameObject.IsDestroyed` before acting on a reference that can outlive the lifecycle phase. Also check `Active` if the reference might be used during the phase in which destruction was requested:

```csharp
if (target is not null && !target.IsDestroyed && target.Active)
{
    Transform.LookAt(target.Transform.Position);
}
```

Do not move scene GameObjects or scoped services into application singletons unless the singleton clears those references during scene unload. Scene changes destroy all GameObjects and dispose their scope.

## Common ownership rule

Whoever creates a short-lived object should also decide when it is destroyed. A projectile can destroy itself on expiry, a wave controller can destroy its enemies, and scene unload remains the final cleanup boundary.
