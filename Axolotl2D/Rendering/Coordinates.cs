using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>Identifies whether draw coordinates belong to the game world or the screen.</summary>
public enum CoordinateSpace
{
    World,
    Screen
}

/// <summary>Explicit coordinate conversions used by input, UI, and world rendering.</summary>
public static class Coordinates
{
    public static Vector2 WorldToScreen(Vector2 worldPosition, Camera2D camera) => camera.WorldToScreen(worldPosition);
    public static Vector2 ScreenToWorld(Vector2 screenPosition, Camera2D camera) => camera.ScreenToWorld(screenPosition);

    public static Vector2 ScreenToNormalizedDevice(Vector2 screenPosition, Vector2 viewport) =>
        new(screenPosition.X / viewport.X * 2f - 1f, 1f - screenPosition.Y / viewport.Y * 2f);

    public static Vector2 NormalizedDeviceToScreen(Vector2 normalizedPosition, Vector2 viewport) =>
        new((normalizedPosition.X + 1f) * 0.5f * viewport.X, (1f - normalizedPosition.Y) * 0.5f * viewport.Y);
}
