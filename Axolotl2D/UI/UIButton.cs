using Axolotl2D.GameObjects;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Base class for routed pointer and keyboard/controller focus controls.</summary>
public abstract class UISelectable(GameObject gameObject, UIEventSystem events) : Component(gameObject)
{
    internal UITransform Layout { get; private set; } = null!;
    public MouseButton Button { get; set; } = MouseButton.Left;
    public bool Interactable { get; set; } = true;
    public int NavigationOrder { get; set; }
    public float Depth { get; set; }
    public bool IsHovered { get; private set; }
    public bool IsPressed { get; private set; }
    public bool IsFocused { get; private set; }

    public event Action? PointerEntered;
    public event Action? PointerExited;
    public event Action<bool>? PressedChanged;
    public event Action<bool>? FocusChanged;

    public override void Awake() => Layout = GameObject.GetComponent<UITransform>()
        ?? throw new InvalidOperationException($"{GetType().Name} requires a UITransform.");
    public override void OnEnable() => events.Add(this);
    public override void OnDisable() { events.Remove(this); ResetState(); }

    protected internal abstract void Activate();
    protected internal virtual void PointerMoved(Vector2 position) { }

    internal void SetHovered(bool value)
    {
        if (value == IsHovered) return;
        IsHovered = value;
        if (value) PointerEntered?.Invoke(); else PointerExited?.Invoke();
    }

    internal void SetPressed(bool value)
    {
        if (value == IsPressed) return;
        IsPressed = value;
        PressedChanged?.Invoke(value);
    }

    internal void SetFocused(bool value)
    {
        if (value == IsFocused) return;
        IsFocused = value;
        FocusChanged?.Invoke(value);
    }

    private void ResetState()
    {
        SetHovered(false);
        SetPressed(false);
        SetFocused(false);
    }
}

/// <summary>A routed pointer and focus-activated button.</summary>
public sealed class UIButton(GameObject gameObject, UIEventSystem events) : UISelectable(gameObject, events)
{
    public event Action? Clicked;
    protected internal override void Activate() => Clicked?.Invoke();
}

/// <summary>A binary UI value changed by pointer or focus activation.</summary>
public sealed class UIToggle(GameObject gameObject, UIEventSystem events) : UISelectable(gameObject, events)
{
    public bool Value { get; private set; }
    public event Action<bool>? ValueChanged;
    public void SetValue(bool value, bool notify = true)
    {
        if (Value == value) return;
        Value = value;
        if (notify) ValueChanged?.Invoke(value);
    }
    protected internal override void Activate() => SetValue(!Value);
}

/// <summary>A horizontal value slider supporting pointer drag and focus activation.</summary>
public sealed class UISlider(GameObject gameObject, UIEventSystem events) : UISelectable(gameObject, events)
{
    private float value;
    public float Minimum { get; set; }
    public float Maximum { get; set; } = 1f;
    public float Step { get; set; } = 0.1f;
    public float Value { get => value; set => SetValue(value); }
    public event Action<float>? ValueChanged;

    public void SetValue(float newValue, bool notify = true)
    {
        if (Maximum <= Minimum) throw new InvalidOperationException("UISlider.Maximum must be greater than Minimum.");
        newValue = Math.Clamp(newValue, Minimum, Maximum);
        if (Math.Abs(newValue - value) < float.Epsilon) return;
        value = newValue;
        if (notify) ValueChanged?.Invoke(value);
    }

    protected internal override void Activate() => SetValue(value + Step > Maximum ? Minimum : value + Step);
    protected internal override void PointerMoved(Vector2 position)
    {
        if (!IsPressed) return;
        var progress = Math.Clamp((position.X - Layout.Rect.Position.X) / Layout.Rect.Size.X, 0f, 1f);
        SetValue(Minimum + (Maximum - Minimum) * progress);
    }
}
