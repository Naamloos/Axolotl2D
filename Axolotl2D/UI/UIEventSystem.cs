using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.UI;

/// <summary>Routes one pointer to the topmost control and owns keyboard/controller focus.</summary>
public sealed class UIEventSystem(Game game)
{
    private readonly List<UISelectable> controls = [];
    private UISelectable? hovered;
    private UISelectable? pressed;
    private bool pointerWasDown;
    private bool nextWasDown;
    private bool previousWasDown;
    private bool submitWasDown;

    public UISelectable? Focused { get; private set; }

    internal void Add(UISelectable control) { if (!controls.Contains(control)) controls.Add(control); }
    internal void Remove(UISelectable control)
    {
        controls.Remove(control);
        if (ReferenceEquals(hovered, control)) hovered = null;
        if (ReferenceEquals(pressed, control)) pressed = null;
        if (ReferenceEquals(Focused, control)) SetFocus(null);
    }

    public void SetFocus(UISelectable? control)
    {
        if (control is not null && (!controls.Contains(control) || !control.Interactable))
            throw new InvalidOperationException("Focus requires an active, interactable control in this scene.");
        if (ReferenceEquals(Focused, control)) return;
        Focused?.SetFocused(false);
        Focused = control;
        Focused?.SetFocused(true);
    }

    public void MoveFocus(int direction)
    {
        var ordered = controls.Where(control => control.Interactable && control.IsActiveAndEnabled)
            .OrderBy(control => control.NavigationOrder).ToArray();
        if (ordered.Length == 0) { SetFocus(null); return; }
        var index = Focused is null ? (direction >= 0 ? -1 : 0) : Array.IndexOf(ordered, Focused);
        SetFocus(ordered[(index + Math.Sign(direction) + ordered.Length) % ordered.Length]);
    }

    public void Submit() => Focused?.Activate();

    internal void Update()
    {
        var mouse = game.GetMouse();
        var pointer = mouse?.Position ?? Vector2.Zero;
        var nextHovered = mouse is null ? null : controls
            .Where(control => control.Interactable && control.IsActiveAndEnabled &&
                control.Layout.Rect.Contains(pointer) &&
                (control.Layout.ResolveClip()?.Contains(pointer) ?? true))
            .OrderByDescending(control => control.Depth)
            .ThenByDescending(control => controls.IndexOf(control))
            .FirstOrDefault();
        if (!ReferenceEquals(nextHovered, hovered))
        {
            hovered?.SetHovered(false);
            hovered = nextHovered;
            hovered?.SetHovered(true);
        }

        var pointerDown = mouse is not null && (pressed ?? hovered) is { } target && mouse.IsButtonPressed(target.Button);
        if (pointerDown && !pointerWasDown)
        {
            pressed = hovered;
            pressed?.SetPressed(true);
            if (pressed is not null) SetFocus(pressed);
        }
        pressed?.PointerMoved(pointer);
        if (!pointerDown && pointerWasDown)
        {
            var released = pressed;
            pressed?.SetPressed(false);
            pressed = null;
            if (released is not null && ReferenceEquals(released, hovered)) released.Activate();
        }
        pointerWasDown = pointerDown;

        var keyboard = game.GetKeyboard();
        var gamepad = game.GetGamepad();
        var nextDown = keyboard?.IsKeyPressed(Key.Tab) == true || keyboard?.IsKeyPressed(Key.Down) == true ||
            Pressed(gamepad, ButtonName.DPadDown);
        var previousDown = keyboard?.IsKeyPressed(Key.Up) == true || Pressed(gamepad, ButtonName.DPadUp);
        var submitDown = keyboard?.IsKeyPressed(Key.Enter) == true || keyboard?.IsKeyPressed(Key.Space) == true ||
            Pressed(gamepad, ButtonName.A);
        if (nextDown && !nextWasDown) MoveFocus(1);
        if (previousDown && !previousWasDown) MoveFocus(-1);
        if (submitDown && !submitWasDown) Submit();
        nextWasDown = nextDown;
        previousWasDown = previousDown;
        submitWasDown = submitDown;
    }

    private static bool Pressed(IGamepad? gamepad, ButtonName name) =>
        gamepad?.Buttons.FirstOrDefault(button => button.Name == name).Pressed == true;
}
