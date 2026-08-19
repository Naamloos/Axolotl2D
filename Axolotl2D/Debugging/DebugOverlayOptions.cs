namespace Axolotl2D.Debugging;

/// <summary>Controls the development overlay installed by a game-host extension.</summary>
public sealed class DebugOverlayOptions(bool enabled = false)
{
    public bool Enabled { get; } = enabled;
    public bool DrawPhysics { get; set; } = true;
    public bool DrawCollisionBounds { get; set; } = true;
}
