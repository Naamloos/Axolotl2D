using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>A texture and named regions packed into it for cross-sprite batching.</summary>
public sealed class TextureAtlas
{
    private readonly IReadOnlyDictionary<string, TextureRegion> regions;

    internal TextureAtlas(Texture2D texture, Texture2D? normalMap,
        IReadOnlyDictionary<string, TextureRegion> regions)
    {
        Texture = texture;
        NormalMap = normalMap;
        this.regions = regions;
    }

    public Texture2D Texture { get; }
    public Texture2D? NormalMap { get; }
    public IReadOnlyDictionary<string, TextureRegion> Regions => regions;

    public Sprite GetSprite(string name)
    {
        if (!regions.TryGetValue(name, out var region))
            throw new KeyNotFoundException($"Texture atlas region '{name}' does not exist.");
        return new Sprite(Texture, region) { NormalMap = NormalMap };
    }
}

/// <summary>Packs source textures into one atlas while extruding edge pixels into its padding.</summary>
public sealed class TextureAtlasBuilder
{
    private readonly List<Entry> entries = [];
    private readonly HashSet<string> names = new(StringComparer.Ordinal);

    public TextureAtlasBuilder Add(string name, Texture2D texture, Texture2D? normalMap = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.PixelSpan.IsEmpty)
            throw new InvalidOperationException("Only textures that still have CPU pixel data can be added to an atlas.");
        if (normalMap is not null &&
            (normalMap.Width != texture.Width || normalMap.Height != texture.Height || normalMap.PixelSpan.IsEmpty))
            throw new ArgumentException("A normal map must have the same dimensions and available pixels as its color texture.",
                nameof(normalMap));
        if (!names.Add(name))
            throw new ArgumentException($"Texture atlas region '{name}' is already registered.", nameof(name));
        entries.Add(new(name, texture, normalMap));
        return this;
    }

    public TextureAtlas Build(int maximumSize = 2048, int padding = 1)
    {
        if (entries.Count == 0)
            throw new InvalidOperationException("A texture atlas requires at least one texture.");
        if (maximumSize <= 0 || (maximumSize & maximumSize - 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSize), "The maximum atlas size must be a power of two.");
        if (padding < 0)
            throw new ArgumentOutOfRangeException(nameof(padding));
        if (padding > maximumSize / 2)
            throw new ArgumentOutOfRangeException(nameof(padding), "Padding must leave room for source pixels.");

        var ordered = entries.OrderByDescending(entry => entry.Texture.Height).ToArray();
        var minimumWidth = ordered.Max(entry => entry.Texture.Width + padding * 2);
        if (minimumWidth > maximumSize)
            throw new InvalidOperationException("A source texture is wider than the requested atlas size.");

        Placement[]? placements = null;
        var width = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, minimumWidth));
        var usedHeight = 0;
        while (width <= maximumSize)
        {
            placements = Pack(ordered, width, padding, out usedHeight);
            if (usedHeight <= maximumSize) break;
            placements = null;
            width *= 2;
        }
        if (placements is null)
            throw new InvalidOperationException($"The textures do not fit inside a {maximumSize}x{maximumSize} atlas.");

        var height = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, usedHeight));
        var length = checked(width * height * 4);
        var colorPixels = MemoryOwner<byte>.Allocate(length, AllocationMode.Clear);
        MemoryOwner<byte>? normalPixels = null;
        try
        {
            var colorRows = colorPixels.Span.AsSpan2D(height, width * 4);
            var hasNormals = ordered.Any(entry => entry.NormalMap is not null);
            Span2D<byte> normalRows = default;
            if (hasNormals)
            {
                normalPixels = MemoryOwner<byte>.Allocate(length);
                FillFlatNormal(normalPixels.Span);
                normalRows = normalPixels.Span.AsSpan2D(height, width * 4);
            }

            var regions = new Dictionary<string, TextureRegion>(entries.Count, StringComparer.Ordinal);
            for (var index = 0; index < ordered.Length; index++)
            {
                var entry = ordered[index];
                var placement = placements[index];
                CopyWithPadding(entry.Texture.PixelSpan, entry.Texture.Width, entry.Texture.Height,
                    colorRows, placement.X, placement.Y, padding);
                if (entry.NormalMap is not null)
                    CopyWithPadding(entry.NormalMap.PixelSpan, entry.NormalMap.Width, entry.NormalMap.Height,
                        normalRows, placement.X, placement.Y, padding);
                regions.Add(entry.Name,
                    new TextureRegion(placement.X, placement.Y, entry.Texture.Width, entry.Texture.Height));
            }

            var colorTexture = new Texture2D(width, height, colorPixels);
            colorPixels = null!;
            Texture2D? normalTexture = null;
            if (normalPixels is not null)
            {
                normalTexture = new Texture2D(width, height, normalPixels);
                normalPixels = null;
            }
            return new(colorTexture, normalTexture, regions);
        }
        finally
        {
            colorPixels?.Dispose();
            normalPixels?.Dispose();
        }
    }

    private static Placement[] Pack(Entry[] ordered, int width, int padding, out int usedHeight)
    {
        var placements = new Placement[ordered.Length];
        var x = 0;
        var y = 0;
        var rowHeight = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            var entry = ordered[index];
            var boxWidth = entry.Texture.Width + padding * 2;
            var boxHeight = entry.Texture.Height + padding * 2;
            if (x + boxWidth > width)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }
            placements[index] = new(x + padding, y + padding);
            x += boxWidth;
            rowHeight = Math.Max(rowHeight, boxHeight);
        }
        usedHeight = y + rowHeight;
        return placements;
    }

    private static void CopyWithPadding(ReadOnlySpan<byte> source, int width, int height,
        Span2D<byte> destination, int targetX, int targetY, int padding)
    {
        var sourceRows = source.AsSpan2D(height, width * 4);
        var targetByteX = targetX * 4;
        var rowBytes = width * 4;
        for (var row = 0; row < height; row++)
            sourceRows.GetRowSpan(row).CopyTo(destination.GetRowSpan(targetY + row).Slice(targetByteX, rowBytes));

        for (var offset = 1; offset <= padding; offset++)
        {
            destination.GetRowSpan(targetY).Slice(targetByteX, rowBytes)
                .CopyTo(destination.GetRowSpan(targetY - offset).Slice(targetByteX, rowBytes));
            destination.GetRowSpan(targetY + height - 1).Slice(targetByteX, rowBytes)
                .CopyTo(destination.GetRowSpan(targetY + height - 1 + offset).Slice(targetByteX, rowBytes));
        }

        for (var row = targetY - padding; row < targetY + height + padding; row++)
        {
            var target = destination.GetRowSpan(row);
            for (var offset = 1; offset <= padding; offset++)
            {
                target.Slice(targetByteX, 4).CopyTo(target.Slice(targetByteX - offset * 4, 4));
                target.Slice(targetByteX + rowBytes - 4, 4)
                    .CopyTo(target.Slice(targetByteX + rowBytes - 4 + offset * 4, 4));
            }
        }
    }

    private static void FillFlatNormal(Span<byte> pixels)
    {
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 128;
            pixels[index + 1] = 128;
            pixels[index + 2] = 255;
            pixels[index + 3] = 255;
        }
    }

    private sealed record Entry(string Name, Texture2D Texture, Texture2D? NormalMap);
    private readonly record struct Placement(int X, int Y);
}
