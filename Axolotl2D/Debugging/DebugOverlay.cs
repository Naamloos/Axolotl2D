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
    private static readonly Color HeaderColor = Color.Cyan;
    private static readonly Color BodyColor = Color.White;
    private readonly Box2DDebugRenderer physicsRenderer = new(primitives, camera);
    private readonly List<string> sceneLines = [];
    private readonly List<string> runtimeLines = [];
    private readonly Type[] sceneTypes = Assembly.GetEntryAssembly()?.GetTypes()
        .Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(BaseScene)))
        .OrderBy(type => type.Name)
        .ToArray() ?? [];
    private DebugGlyphAtlas? glyphs;
    private IReadOnlyList<AssetInfo> loadedAssets = [];
    private ulong assetRefreshFrame;
    private bool hasAssetSnapshot;
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
        BuildSceneLines(scene);
        BuildRuntimeLines(physics, frameDelta, frameRate);
        DrawColumn(sceneLines, Vector2.Zero, new Vector2(leftWidth, viewport.Y), rightAligned: false);
        DrawColumn(runtimeLines, new Vector2(leftWidth + gap, 0f),
            new Vector2(rightWidth, viewport.Y), rightAligned: true);
    }

    private void BuildSceneLines(BaseScene? scene)
    {
        sceneLines.Clear();
        sceneLines.Add("AXOLOTL2D DEBUG");
        sceneLines.Add(string.Empty);
        sceneLines.Add($"SCENES ({sceneTypes.Length})");
        if (scene is null)
            sceneLines.Add("  (simple host: no scene scope)");
        foreach (var type in sceneTypes)
            sceneLines.Add($"{(scene?.GetType() == type ? '*' : ' ')} {type.Name}");

        if (scene is null)
            return;

        sceneLines.Add($"Scope #{scene.ScopeServices.GetHashCode():X8}  Loaded={scene.IsLoaded}");
        sceneLines.Add(string.Empty);
        sceneLines.Add($"GAMEOBJECTS ({scene.GameObjects.Count})");
        foreach (var gameObject in scene.GameObjects)
        {
            var transform = gameObject.Transform;
            sceneLines.Add(Trim($"{(gameObject.Active ? '+' : '-')} {gameObject.Name}  " +
                $"P({transform.Position.X:0.#},{transform.Position.Y:0.#}) " +
                $"R{transform.Rotation * 180f / MathF.PI:0.#} " +
                $"S({transform.LossyScale.X:0.##},{transform.LossyScale.Y:0.##})", 74));
            foreach (var component in gameObject.Components)
            {
                var state = component.IsActiveAndEnabled ? "active"
                    : component.Enabled ? "inactive" : "disabled";
                sceneLines.Add(Trim($"    {component.GetType().Name} [{state}, " +
                    $"{(component.HasStarted ? "started" : "not started")}]", 74));
            }
        }
    }

    private void BuildRuntimeLines(PhysicsWorld? physics, double frameDelta, double frameRate)
    {
        var statistics = rendering.Statistics;
        if (!hasAssetSnapshot || time.FrameCount - assetRefreshFrame >= 30)
        {
            loadedAssets = assets.GetLoadedAssets();
            assetRefreshFrame = time.FrameCount;
            hasAssetSnapshot = true;
        }
        runtimeLines.Clear();
        runtimeLines.Add("FRAME");
        runtimeLines.Add($"FPS {frameRate:0.0}  interval {frameDelta * 1000d:0.00} ms");
        runtimeLines.Add($"Update {game.LastUpdateMilliseconds:0.00} ms  Draw {game.LastDrawMilliseconds:0.00} ms");
        runtimeLines.Add($"GPU {statistics.GpuMilliseconds:0.00} ms  Frame {time.FrameCount}");
        runtimeLines.Add($"CPU cull {statistics.CpuCullingMilliseconds:0.00}  sort {statistics.CpuSortingMilliseconds:0.00} ms");
        runtimeLines.Add($"CPU vertices {statistics.CpuVertexBuildMilliseconds:0.00}  submit {statistics.CpuSubmissionMilliseconds:0.00} ms");
        runtimeLines.Add(string.Empty);
        runtimeLines.Add("DRAW SUBMISSIONS");
        runtimeLines.Add($"Commands {statistics.DrawCommands}  Culled {statistics.CulledCommands}");
        runtimeLines.Add($"GPU draws {statistics.DrawSubmissions}  Triangles {statistics.Triangles}");
        runtimeLines.Add($"Vertex upload {statistics.UploadedVertexBytes / 1024d:0.#} KiB  Textures {statistics.UploadedTextures}");
        runtimeLines.Add(string.Empty);
        runtimeLines.Add($"LOADED ASSETS ({loadedAssets.Count})");
        foreach (var asset in loadedAssets)
            runtimeLines.Add(Trim($"  [{asset.State}] {asset.Type.Name}: {asset.Key}", 62));

        runtimeLines.Add(string.Empty);
        if (physics is null)
        {
            runtimeLines.Add("PHYSICS (no scene world)");
            return;
        }

        var counters = physics.Counters;
        runtimeLines.Add($"PHYSICS BODIES ({physics.Bodies.Count})");
        runtimeLines.Add($"Shapes {counters.shapeCount}  Contacts {counters.contactCount}  Joints {counters.jointCount}");
        foreach (var body in physics.Bodies)
            runtimeLines.Add(Trim($"  {body.GameObject.Name}: {body.Type}, shapes={body.ShapeCount}, " +
                $"{(body.IsActiveAndEnabled ? "active" : "inactive")}", 62));
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
                index == 0 ? HeaderColor : BodyColor, PanelDepth + 2f, TextScale);
        }
    }

    private static string Trim(string value, int maximum) =>
        value.Length <= maximum ? value : value[..Math.Max(0, maximum - 3)] + "...";
}
