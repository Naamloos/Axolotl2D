using Axolotl2D.GameObjects;
using Silk.NET.Input;

namespace Axolotl2D.UI;

/// <summary>Provides hover, press, and click interaction for a UI rectangle.</summary>
public sealed class UIButton(GameObject gameObject, Game game) : Component(gameObject)
{
    private UITransform transform = null!;
    private bool pointerWasDown;
    private bool pressStartedHere;

    public MouseButton Button { get; set; } = MouseButton.Left;
    public bool Interactable { get; set; } = true;
    public bool IsHovered { get; private set; }
    public bool IsPressed { get; private set; }

    public event Action? Clicked;
    public event Action? PointerEntered;
    public event Action? PointerExited;
    public event Action<bool>? PressedChanged;

    public override void Start() => transform = GameObject.GetComponent<UITransform>()
        ?? throw new InvalidOperationException("UIButton requires a UITransform on the same GameObject.");

    public override void Update(double deltaTime)
    {
        var mouse = game.GetMouse();
        if (mouse is null || !Interactable)
        {
            ResetState();
            return;
        }

        var hovered = transform.Rect.Contains(mouse.Position);
        if (hovered != IsHovered)
        {
            IsHovered = hovered;
            if (hovered) PointerEntered?.Invoke(); else PointerExited?.Invoke();
        }

        var pointerDown = mouse.IsButtonPressed(Button);
        if (pointerDown && !pointerWasDown)
            pressStartedHere = hovered;

        var pressed = pointerDown && pressStartedHere;
        if (pressed != IsPressed)
        {
            IsPressed = pressed;
            PressedChanged?.Invoke(pressed);
        }

        if (!pointerDown && pointerWasDown)
        {
            if (pressStartedHere && hovered)
                Clicked?.Invoke();
            pressStartedHere = false;
        }
        pointerWasDown = pointerDown;
    }

    public override void OnDisable() => ResetState();

    private void ResetState()
    {
        if (IsHovered)
            PointerExited?.Invoke();
        if (IsPressed)
            PressedChanged?.Invoke(false);
        IsHovered = false;
        IsPressed = false;
        pointerWasDown = false;
        pressStartedHere = false;
    }
}
