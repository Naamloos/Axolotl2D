using System.Numerics;

namespace Axolotl2D;

/// <summary>Initial window and display settings.</summary>
public sealed class GameWindowOptions
{
    public string Title { get; set; } = "";
    public Vector2 Size { get; set; } = new(1080f, 720f);
    public GameWindowMode Mode { get; set; } = GameWindowMode.Windowed;
    public bool Resizable { get; set; } = true;
    public bool VSync { get; set; }
    public double MaximumDrawRate { get; set; } = 120d;
    public double MaximumUpdateRate { get; set; } = 120d;
    public bool ShowFramerateInTitle { get; set; } = true;
}

public enum GameWindowMode
{
    Windowed,
    BorderlessFullscreen,
    Fullscreen
}
