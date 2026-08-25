using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>An opt-in uniform grid for cheaply submitting only camera-visible static sprites.</summary>
public sealed class SpatialSpriteIndex
{
    private readonly float cellSize;
    private readonly Dictionary<long, List<int>> cells = [];
    private readonly List<Entry> entries = [];
    private int queryId;

    public int Count => entries.Count;

    public SpatialSpriteIndex(float cellSize = 256f)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
        this.cellSize = cellSize;
    }

    public void Add(Sprite sprite, Matrix3x2 transform, Color? tint = null, float depth = 0f,
        uint lightingLayer = 1)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        var bounds = Bounds(sprite, transform);
        var entryIndex = entries.Count;
        entries.Add(new(sprite, transform, tint ?? Color.White, depth, lightingLayer, bounds.Min, bounds.Max));
        var minimum = Cell(bounds.Min);
        var maximum = Cell(bounds.Max);
        for (var y = minimum.Y; y <= maximum.Y; y++)
            for (var x = minimum.X; x <= maximum.X; x++)
            {
                var key = Key(x, y);
                if (!cells.TryGetValue(key, out var bucket)) cells.Add(key, bucket = []);
                bucket.Add(entryIndex);
            }
    }

    public void Clear()
    {
        cells.Clear();
        entries.Clear();
        queryId = 0;
    }

    public void DrawVisible(SpriteBatch spriteBatch, Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(camera);
        var bounds = camera.VisibleWorldBounds;
        var minimum = Cell(bounds.Min);
        var maximum = Cell(bounds.Max);
        if (++queryId == int.MaxValue)
        {
            for (var index = 0; index < entries.Count; index++) entries[index].QueryId = 0;
            queryId = 1;
        }
        for (var y = minimum.Y; y <= maximum.Y; y++)
            for (var x = minimum.X; x <= maximum.X; x++)
            {
                if (!cells.TryGetValue(Key(x, y), out var bucket)) continue;
                for (var index = 0; index < bucket.Count; index++)
                {
                    var entry = entries[bucket[index]];
                    if (entry.QueryId == queryId) continue;
                    entry.QueryId = queryId;
                    if ((entry.LightingLayer & camera.CullingMask) == 0 ||
                        entry.Maximum.X < bounds.Min.X || entry.Minimum.X > bounds.Max.X ||
                        entry.Maximum.Y < bounds.Min.Y || entry.Minimum.Y > bounds.Max.Y) continue;
                    spriteBatch.Draw(entry.Sprite, entry.Transform, entry.Tint, CoordinateSpace.World,
                        entry.Depth, entry.LightingLayer);
                }
            }
    }

    private (int X, int Y) Cell(Vector2 position) =>
        ((int)MathF.Floor(position.X / cellSize), (int)MathF.Floor(position.Y / cellSize));

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    private static (Vector2 Min, Vector2 Max) Bounds(Sprite sprite, Matrix3x2 transform)
    {
        var size = sprite.Size;
        var origin = sprite.Origin * size;
        var extent = size / 2f;
        var center = Vector2.Transform(extent - origin, transform);
        var worldExtent = new Vector2(
            MathF.Abs(transform.M11) * extent.X + MathF.Abs(transform.M21) * extent.Y,
            MathF.Abs(transform.M12) * extent.X + MathF.Abs(transform.M22) * extent.Y);
        return (center - worldExtent, center + worldExtent);
    }

    private sealed class Entry(Sprite sprite, Matrix3x2 transform, Color tint, float depth, uint lightingLayer,
        Vector2 minimum, Vector2 maximum)
    {
        public Sprite Sprite { get; } = sprite;
        public Matrix3x2 Transform { get; } = transform;
        public Color Tint { get; } = tint;
        public float Depth { get; } = depth;
        public uint LightingLayer { get; } = lightingLayer;
        public Vector2 Minimum { get; } = minimum;
        public Vector2 Maximum { get; } = maximum;
        public int QueryId { get; set; }
    }
}
