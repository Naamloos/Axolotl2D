# Transforms and Hierarchies

Every GameObject owns a `Transform` with 2D position, rotation, scale, and optional parenting. Local values are relative to the parent; world values include the full hierarchy.

## Local and world properties

```csharp
transform.LocalPosition = new Vector2(40, 20);
transform.LocalRotation = MathF.PI / 2f;
transform.LocalScale = new Vector2(2, 2);

Vector2 worldPosition = transform.Position;
float worldRotation = transform.Rotation;
Vector2 approximateWorldScale = transform.LossyScale;
Matrix3x2 worldMatrix = transform.WorldMatrix;
```

Rotation values are radians. `LocalMatrix` applies scale, then rotation, then translation. `WorldMatrix` composes the local matrix with every parent.

`LossyScale` is the component-wise accumulated scale. It is convenient for ordinary 2D hierarchies but does not represent skew that can arise from mixed non-uniform scales and rotations.

## Translate and rotate

```csharp
transform.Translate(new Vector2(10, 0));
transform.Rotate(MathF.PI / 8f);
```

The default `Translate` amount is interpreted in the parent's coordinate space. Pass `localSpace: true` to move along the transform's current right and up directions:

```csharp
transform.Translate(new Vector2(speed * deltaTime, 0), localSpace: true);
```

`Right` points along the transform's local positive X direction. `Up` follows the screen-oriented 2D convention and points along local negative Y at zero rotation.

## Look at a target

```csharp
transform.LookAt(target.Transform.Position);
```

`LookAt` rotates local positive X toward a world-space target and compensates for parent rotation.

## Parent objects

```csharp
weapon.Transform.SetParent(player.Transform);
```

Parenting preserves the child's current world position, rotation, and scale by default. Pass `worldPositionStays: false` to retain its local values instead:

```csharp
weapon.Transform.SetParent(player.Transform, worldPositionStays: false);
weapon.Transform.LocalPosition = new Vector2(12, 4);
```

Assigning the `Parent` property is equivalent to `SetParent(newParent)` with world preservation. Cycles are rejected. `Children` provides a read-only view of direct child transforms.

Destroying a GameObject detaches its transform. Its children keep their world transforms and become root transforms; they are not destroyed automatically.

## Convert points

```csharp
Vector2 muzzleInWorld = weapon.Transform.TransformPoint(new Vector2(16, 0));
Vector2 pointInWeaponSpace = weapon.Transform.InverseTransformPoint(worldPoint);
```

`InverseTransformPoint` throws if the world matrix cannot be inverted, such as when a transform has zero scale.

Use transform conversion for attachment points, local interaction regions, and procedural child placement. Use [Camera and Coordinate Systems](camera-and-coordinate-systems.md) when converting between world and screen positions.
