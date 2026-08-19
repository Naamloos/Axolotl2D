using Axolotl2D.Assets;
using Axolotl2D.Physics;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.Timing;
using System.Numerics;
using System.Reflection;

namespace Axolotl2D.Debugging;

/// <summary>Draws runtime scene, lifecycle, rendering, asset, timing, and physics inspection data.</summary>
public sealed class DebugOverlay(
    DebugOverlayOptions options,
    Game game,
    AssetManager assets,
    IRendering rendering,
    SpriteBatch spriteBatch,
    PrimitiveBatch primitives,
    Camera2D camera,
    TimeService time)
{
    private const float PanelDepth = 100_000f;
    private const float TextScale = 0.75f;
    private readonly Box2DDebugRenderer physicsRenderer = new(primitives, camera);
    private readonly Type[] sceneTypes = Assembly.GetEntryAssembly()?.GetTypes()
        .Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(BaseScene)))
        .OrderBy(type => type.Name)
        .ToArray() ?? [];
    private DebugGlyphAtlas? glyphs;
    private bool simpleAttached;

    public bool Enabled => options.Enabled;

    internal void Render(BaseScene scene, PhysicsWorld physics, double frameDelta, double frameRate)
    {
        if (!Enabled)
            return;
        if (options.DrawPhysics)
            physicsRenderer.Draw(physics, options.DrawCollisionBounds);
        DrawPanels(scene, physics, frameDelta, frameRate);
    }

    internal void AttachSimpleHost()
    {
        if (!Enabled || simpleAttached)
            return;
        simpleAttached = true;
        game.OnLoad += AttachSimpleDraw;
        game.Closing += DetachSimpleHost;
    }

    internal void DetachSimpleHost()
    {
        if (!simpleAttached)
            return;
        simpleAttached = false;
        game.OnLoad -= AttachSimpleDraw;
        game.OnDraw -= RenderSimple;
        game.Closing -= DetachSimpleHost;
    }

    private void AttachSimpleDraw()
    {
        game.OnDraw += RenderSimple;
        game.OnLoad -= AttachSimpleDraw;
    }

    private void RenderSimple(double frameDelta, double frameRate)
    {
        spriteBatch.Begin();
        try
        {
            DrawPanels(null, null, frameDelta, frameRate);
        }
        finally
        {
            spriteBatch.End();
        }
    }

    private void DrawPanels(BaseScene? scene, PhysicsWorld? physics, double frameDelta, double frameRate)
    {
        glyphs ??= new DebugGlyphAtlas();
        const float gap = 8f;
        var viewport = game.Viewport;
        var leftWidth = MathF.Max(0f, (viewport.X - gap) / 2f);
        var rightWidth = leftWidth;
        DrawColumn(BuildSceneLines(scene), Vector2.Zero, new Vector2(leftWidth, viewport.Y), rightAligned: false);
        DrawColumn(BuildRuntimeLines(physics, frameDelta, frameRate), new Vector2(leftWidth + gap, 0f),
            new Vector2(rightWidth, viewport.Y), rightAligned: true);
    }

    private List<string> BuildSceneLines(BaseScene? scene)
    {
        var lines = new List<string> { "AXOLOTL2D DEBUG", string.Empty };
        lines.Add($"SCENES ({sceneTypes.Length})");
        if (scene is null)
            lines.Add("  (simple host: no scene scope)");
        foreach (var type in sceneTypes)
            lines.Add($"{(scene?.GetType() == type ? '*' : ' ')} {type.Name}");

        if (scene is null)
            return lines;

        lines.Add($"Scope #{scene.ScopeServices.GetHashCode():X8}  Loaded={scene.IsLoaded}");
        lines.Add(string.Empty);
        lines.Add($"GAMEOBJECTS ({scene.GameObjects.Count})");
        foreach (var gameObject in scene.GameObjects)
        {
            var transform = gameObject.Transform;
            lines.Add(Trim($"{(gameObject.Active ? '+' : '-')} {gameObject.Name}  " +
                $"P({transform.Position.X:0.#},{transform.Position.Y:0.#}) " +
                $"R{transform.Rotation * 180f / MathF.PI:0.#} " +
                $"S({transform.LossyScale.X:0.##},{transform.LossyScale.Y:0.##})", 74));
            foreach (var component in gameObject.Components)
            {
                var state = component.IsActiveAndEnabled ? "active"
                    : component.Enabled ? "inactive" : "disabled";
                lines.Add(Trim($"    {component.GetType().Name} [{state}, " +
                    $"{(component.HasStarted ? "started" : "not started")}]", 74));
            }
        }
        return lines;
    }

    private List<string> BuildRuntimeLines(PhysicsWorld? physics, double frameDelta, double frameRate)
    {
        var statistics = rendering.Statistics;
        var loadedAssets = assets.GetLoadedAssets();
        var lines = new List<string>
        {
            "FRAME",
            $"FPS {frameRate:0.0}  interval {frameDelta * 1000d:0.00} ms",
            $"Update {game.LastUpdateMilliseconds:0.00} ms  Draw {game.LastDrawMilliseconds:0.00} ms",
            $"Frame {time.FrameCount}  Fixed {time.FixedFrameCount}  Scale {time.TimeScale:0.##}",
            string.Empty,
            "DRAW SUBMISSIONS",
            $"Commands {statistics.DrawCommands}  GPU draws {statistics.DrawSubmissions}",
            $"Triangles {statistics.Triangles}  GPU textures {statistics.UploadedTextures}",
            string.Empty,
            $"LOADED ASSETS ({loadedAssets.Count})"
        };
        foreach (var asset in loadedAssets)
            lines.Add(Trim($"  [{asset.State}] {asset.Type.Name}: {asset.Key}", 62));

        lines.Add(string.Empty);
        if (physics is null)
        {
            lines.Add("PHYSICS (no scene world)");
            return lines;
        }

        var counters = physics.Counters;
        lines.Add($"PHYSICS BODIES ({physics.Bodies.Count})");
        lines.Add($"Shapes {counters.shapeCount}  Contacts {counters.contactCount}  Joints {counters.jointCount}");
        foreach (var body in physics.Bodies)
            lines.Add(Trim($"  {body.GameObject.Name}: {body.Type}, shapes={body.ShapeCount}, " +
                $"{(body.IsActiveAndEnabled ? "active" : "inactive")}", 62));
        return lines;
    }

    private void DrawColumn(IReadOnlyList<string> lines, Vector2 position, Vector2 size, bool rightAligned)
    {
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var characterWidth = DebugGlyphAtlas.CellWidth * TextScale;
        var lineHeight = DebugGlyphAtlas.CellHeight * TextScale;
        var availableRows = Math.Max(0, (int)((size.Y - 12f) / lineHeight));
        var maximumCharacters = Math.Max(1, (int)((size.X - 12f) / characterWidth));
        var rows = Math.Min(lines.Count, availableRows);
        for (var index = 0; index < rows; index++)
        {
            var text = index == rows - 1 && rows < lines.Count
                ? $"... {lines.Count - rows + 1} more"
                : Trim(lines[index], maximumCharacters);
            text = Trim(text, maximumCharacters);
            var x = rightAligned
                ? position.X + size.X - 6f - text.Length * characterWidth
                : position.X + 6f;
            glyphs!.Draw(spriteBatch, text, new Vector2(x, position.Y + 6f + index * lineHeight),
                index == 0 ? Color.Cyan : Color.White, PanelDepth + 2f, TextScale);
        }
    }

    private static string Trim(string value, int maximum) =>
        value.Length <= maximum ? value : value[..Math.Max(0, maximum - 3)] + "...";
}
