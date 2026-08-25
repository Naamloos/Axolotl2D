using Axolotl2D.Assets;
using CommunityToolkit.HighPerformance;
using SkiaSharp;
using System.Numerics;
using Axolotl2D.Timing;

namespace Axolotl2D.Rendering;

/// <summary>Shapes scalable font text and batches cached strings through shared atlas pages.</summary>
public sealed class TextRenderer
{
    private const int AtlasSize = 1024;
    private const int AtlasPadding = 1;
    private const int MaximumAtlasPages = 4;
    private readonly Dictionary<(FontAsset Font, float Size, string Text), Texture2D> standaloneCache = [];
    private readonly Dictionary<(FontAsset Font, float Size, string Text), CachedText> atlasCache = [];
    private readonly List<TextAtlasPage> pages = [];
    private readonly Dictionary<FontAsset, SdfGlyphAtlas> sdfAtlases = new(ReferenceEqualityComparer.Instance);
    private readonly TimeService? time;
    private long accessCounter;

    public TextRenderer() { }
    public TextRenderer(TimeService time) => this.time = time;

    /// <summary>Renders an exact-size standalone texture for compatibility with texture-oriented callers.</summary>
    public Texture2D Render(FontAsset fontAsset, string text, float fontSize)
    {
        Validate(fontAsset, text, fontSize);
        var key = (fontAsset, fontSize, text);
        if (standaloneCache.TryGetValue(key, out var cached)) return cached;
        using var raster = Rasterize(fontAsset, text, fontSize);
        var texture = CreateStandalone(raster);
        standaloneCache.Add(key, texture);
        return texture;
    }

    internal Sprite RenderSprite(FontAsset fontAsset, string text, float fontSize) =>
        GetAtlasText(fontAsset, text, fontSize).Sprite;

    internal Vector2 Measure(FontAsset font, string text, float fontSize)
    {
        Validate(font, text, fontSize);
        if (!IsSdfCompatible(text)) return RenderSprite(font, text, fontSize).Size;
        if (!sdfAtlases.TryGetValue(font, out var atlas))
            sdfAtlases.Add(font, atlas = new SdfGlyphAtlas(font));
        return atlas.Measure(text, fontSize);
    }

    private CachedText GetAtlasText(FontAsset fontAsset, string text, float fontSize)
    {
        Validate(fontAsset, text, fontSize);
        var key = (fontAsset, fontSize, text);
        if (atlasCache.TryGetValue(key, out var cached))
        {
            cached.Page?.Touch(++accessCounter, time?.FrameCount);
            return cached;
        }

        using var raster = Rasterize(fontAsset, text, fontSize);
        if (raster.Width + AtlasPadding * 2 > AtlasSize || raster.Height + AtlasPadding * 2 > AtlasSize)
        {
            var texture = CreateStandalone(raster);
            cached = new(new Sprite(texture) { Origin = Vector2.Zero }, null);
            atlasCache.Add(key, cached);
            return cached;
        }

        TextAtlasPage? page = null;
        TextureRegion region = default;
        for (var index = 0; index < pages.Count; index++)
            if (pages[index].TryAllocate(raster.Width, raster.Height, out region))
            {
                page = pages[index];
                break;
            }

        if (page is null)
        {
            if (pages.Count < MaximumAtlasPages)
            {
                page = new(AtlasSize);
                pages.Add(page);
            }
            else
            {
                var frame = time?.FrameCount;
                page = frame is null ? null : pages
                    .Where(candidate => candidate.LastUsedFrame != frame)
                    .MinBy(candidate => candidate.LastUsed);
                if (page is null)
                {
                    page = new(AtlasSize);
                    pages.Add(page);
                }
                else
                {
                    Evict(page);
                    page.Clear();
                }
            }
            if (!page.TryAllocate(raster.Width, raster.Height, out region))
                throw new InvalidOperationException("Text does not fit inside an empty atlas page.");
        }

        page.Copy(raster, region);
        page.Touch(++accessCounter, time?.FrameCount);
        cached = new(new Sprite(page.Texture, region) { Origin = Vector2.Zero }, page);
        atlasCache.Add(key, cached);
        return cached;
    }

    public void Draw(SpriteBatch spriteBatch, FontAsset font, string text, float fontSize, Vector2 position,
        Color? color = null, CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        Validate(font, text, fontSize);
        if (IsSdfCompatible(text))
        {
            if (!sdfAtlases.TryGetValue(font, out var atlas))
                sdfAtlases.Add(font, atlas = new SdfGlyphAtlas(font));
            atlas.Draw(spriteBatch, text, fontSize, position, color ?? Color.White, space, depth);
            return;
        }
        spriteBatch.Draw(RenderSprite(font, text, fontSize), position,
            tint: color, space: space, depth: depth);
    }

    private static bool IsSdfCompatible(string text)
    {
        for (var index = 0; index < text.Length; index++)
            if (text[index] is not ('\n' or '\r' or '\t') && (text[index] < ' ' || text[index] > '~'))
                return false;
        return true;
    }

    private void Evict(TextAtlasPage page)
    {
        var stale = new List<(FontAsset Font, float Size, string Text)>();
        foreach (var entry in atlasCache)
            if (ReferenceEquals(entry.Value.Page, page))
                stale.Add(entry.Key);
        foreach (var key in stale)
            atlasCache.Remove(key);
    }

    private static Texture2D CreateStandalone(RasterizedText raster)
    {
        var pixels = new byte[raster.Width * raster.Height * 4];
        CopyAlpha(raster.Alpha, pixels.AsSpan().AsSpan2D(raster.Height, raster.Width * 4), 0, 0);
        return new(raster.Width, raster.Height, pixels);
    }

    private static void CopyAlpha(ReadOnlySpan2D<byte> alpha, Span2D<byte> destination, int x, int y)
    {
        var byteX = x * 4;
        for (var row = 0; row < alpha.Height; row++)
        {
            var sourceRow = alpha.GetRowSpan(row);
            var targetRow = destination.GetRowSpan(y + row).Slice(byteX, alpha.Width * 4);
            for (var column = 0; column < alpha.Width; column++)
            {
                var pixel = column * 4;
                targetRow[pixel] = 255;
                targetRow[pixel + 1] = 255;
                targetRow[pixel + 2] = 255;
                targetRow[pixel + 3] = sourceRow[column];
            }
        }
    }

    private static RasterizedText Rasterize(FontAsset fontAsset, string text, float fontSize)
    {
        using var font = new SKFont(fontAsset.Typeface, fontSize);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        font.GetFontMetrics(out var metrics);

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var bounds = lines.Select(line =>
        {
            font.MeasureText(line, out var lineBounds, paint);
            return lineBounds;
        }).ToArray();
        var left = MathF.Min(0, bounds.Min(bound => bound.Left));
        var right = MathF.Max(1, bounds.Max(bound => bound.Right));
        var lineHeight = MathF.Max(1, metrics.Descent - metrics.Ascent + metrics.Leading);
        var width = Math.Max(1, (int)MathF.Ceiling(right - left));
        var height = Math.Max(1, (int)MathF.Ceiling(lineHeight * lines.Length));

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        for (var index = 0; index < lines.Length; index++)
            canvas.DrawText(lines[index], -left, index * lineHeight - metrics.Ascent,
                SKTextAlign.Left, font, paint);
        canvas.Flush();
        return new(bitmap, width, height);
    }

    private static void Validate(FontAsset fontAsset, string text, float fontSize)
    {
        ArgumentNullException.ThrowIfNull(fontAsset);
        ArgumentNullException.ThrowIfNull(text);
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
    }

    private readonly record struct CachedText(Sprite Sprite, TextAtlasPage? Page);

    private sealed class RasterizedText(SKBitmap bitmap, int width, int height) : IDisposable
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public ReadOnlySpan2D<byte> Alpha => bitmap.GetPixelSpan()
            .AsSpan2D(0, Height, Width, bitmap.RowBytes - Width);
        public void Dispose() => bitmap.Dispose();
    }

    private sealed class TextAtlasPage
    {
        private readonly byte[] pixels;
        private readonly int size;
        private int x;
        private int y;
        private int rowHeight;

        public TextAtlasPage(int size)
        {
            this.size = size;
            pixels = new byte[size * size * 4];
            Texture = new(size, size, pixels);
        }

        public Texture2D Texture { get; }
        public long LastUsed { get; private set; }
        public ulong? LastUsedFrame { get; private set; }

        public bool TryAllocate(int width, int height, out TextureRegion region)
        {
            var boxWidth = width + AtlasPadding * 2;
            var boxHeight = height + AtlasPadding * 2;
            var nextX = x;
            var nextY = y;
            var nextRowHeight = rowHeight;
            if (nextX + boxWidth > size)
            {
                nextX = 0;
                nextY += nextRowHeight;
                nextRowHeight = 0;
            }
            if (nextY + boxHeight > size)
            {
                region = default;
                return false;
            }

            region = new(nextX + AtlasPadding, nextY + AtlasPadding, width, height);
            x = nextX + boxWidth;
            y = nextY;
            rowHeight = Math.Max(nextRowHeight, boxHeight);
            return true;
        }

        public void Copy(RasterizedText raster, TextureRegion region)
        {
            CopyAlpha(raster.Alpha, pixels.AsSpan().AsSpan2D(size, size * 4), region.X, region.Y);
            Texture.MarkDirty(region);
        }

        public void Touch(long access, ulong? frame)
        {
            LastUsed = access;
            LastUsedFrame = frame;
        }

        public void Clear()
        {
            pixels.AsSpan().Clear();
            x = 0;
            y = 0;
            rowHeight = 0;
            Texture.MarkDirty(new(0, 0, size, size));
        }
    }
}
