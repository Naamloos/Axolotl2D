# User Interface

Axolotl2D's retained UI is composed from `UITransform`, `UIVisual`, `UIText`, and `UIButton` components. UI coordinates are screen pixels with `(0, 0)` at the top left. Camera movement does not affect them.

## Position UI elements

Every visual or interactive UI GameObject needs a `UITransform`:

```csharp
using Axolotl2D.UI;

var panel = Instantiate("Pause panel");
var layout = panel.AddComponent<UITransform>();
layout.Anchor = new Vector2(0.5f);          // viewport center
layout.Pivot = new Vector2(0.5f);           // element center
layout.AnchoredPosition = Vector2.Zero;
layout.Size = new Vector2(360, 220);
```

`Anchor` is normalized inside the viewport or parent rectangle. `Pivot` is normalized inside the element. `AnchoredPosition` and `Size` use screen pixels, so the example remains centered as the window resizes.

UI elements can form their own layout hierarchy:

```csharp
var childLayout = child.AddComponent<UITransform>();
childLayout.Anchor = new Vector2(0.5f, 1f);
childLayout.Pivot = new Vector2(0.5f, 1f);
childLayout.Size = new Vector2(200, 40);
childLayout.SetParent(layout, screenPositionStays: false);
```

Use `screenPositionStays: false` when the configured anchor and offset should take effect immediately. `Children`, `Parent`, and `Rect` expose the resolved hierarchy and screen rectangle.

## Draw textures or primitive fallbacks

`UIVisual` uses its `Sprite` when one is assigned:

```csharp
var visual = panel.AddComponent<UIVisual>();
visual.Sprite = new Sprite(assets.Get<Texture2D>("pause-panel"));
visual.Color = Color.White;
visual.Depth = 100f;
```

Leave `Sprite` null to draw a primitive shape instead:

```csharp
visual.Sprite = null;
visual.Primitive = UIPrimitive.Rectangle;
visual.Color = new Color(0.04f, 0.06f, 0.1f, 0.92f);
```

The primitive choices are filled or outlined rectangles and circles. `Thickness` controls outlined shapes. This fallback lets menus, HUD panels, focus rings, and simple controls work without texture assets.

Inject `PrimitiveBatch` when a scene or custom component needs direct `FillRectangle`, `DrawRectangle`, `DrawLine`, `FillCircle`, or `DrawCircle` calls. Like every rendering helper, it must be used while the scene sprite batch is open.

## Add text

`UIText` aligns a loaded font inside the same UI rectangle:

```csharp
var label = panel.AddComponent<UIText>();
label.Font = assets.Get<FontAsset>("ui-font");
label.Text = "Paused";
label.FontSize = 28f;
label.HorizontalAlignment = UIHorizontalAlignment.Center;
label.VerticalAlignment = UIVerticalAlignment.Center;
label.Depth = 101f;
```

Configure `Font` before the component starts. `UIText` uses the existing `TextRenderer` cache, so update rapidly changing labels only when their displayed value changes.

## Handle pointer interaction

`UIButton` performs rectangle hit testing against the current mouse and exposes hover, press, and click state:

```csharp
var button = panel.AddComponent<UIButton>();
button.Clicked += ResumeGame;
button.PointerEntered += () => visual.Color = Color.LightGray;
button.PointerExited += () => visual.Color = Color.White;
button.PressedChanged += pressed =>
    visual.Color = pressed ? Color.Gray : Color.White;
```

Change `Button` to use another mouse button or set `Interactable` to suspend interaction. `Clicked` fires only when a press starts inside the rectangle and the matching release also occurs inside it. Disabling the component clears its hover and press state.

## Stretch and arrange layouts

Set different anchor bounds to stretch an element inside its parent:

```csharp
layout.AnchorMin = Vector2.Zero;
layout.AnchorMax = Vector2.One;
layout.OffsetMin = new Vector2(24);
layout.OffsetMax = new Vector2(-24);
```

`MinSize` and `MaxSize` constrain the resolved rectangle. `UILayoutGroup` arranges direct UI children in a horizontal or vertical row with padding, spacing, alignment, and optional expansion.

## Clip and scroll content

Add `UIClip` to a viewport GameObject. Descendant `UIVisual` and `UIText` components intersect ancestor clip rectangles before submitting draw commands.

`UIScrollView` owns a larger `Content` transform and clamps its offset:

```csharp
var scroll = viewportObject.AddComponent<UIScrollView>();
scroll.Content = contentLayout;
scroll.ContentSize = new Vector2(480, 1200);
scroll.Vertical = true;
scroll.ScrollBy(new Vector2(0, 100));
```

The component adds `UIClip` when needed and accepts mouse-wheel scrolling while the pointer lies inside its viewport.

## Route input and focus

`UIEventSystem` sends pointer state to the highest-depth overlapping `UISelectable`. `UIButton`, `UIToggle`, and `UISlider` participate in keyboard and controller focus. Arrow keys, Tab, and controller D-pad buttons change focus. Enter, Space, and controller A activate the focused control.

Use `NavigationOrder` for focus order and `Depth` for pointer arbitration. `FocusChanged` supports focus styling. Games can call `UIEventSystem.SetFocus`, `MoveFocus`, or `Submit` for custom navigation.

## Other controls

- `UIToggle` exposes `Value`, `SetValue`, and `ValueChanged`.
- `UISlider` supports pointer drag, min/max values, steps, and `ValueChanged`.
- `UIProgressBar` draws a clamped horizontal fill with configurable colors.
- `UIScrollView` supplies clipped content offsets and wheel input.
