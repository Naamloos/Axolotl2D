using Axolotl2D.Assets;
using CommunityToolkit.HighPerformance;
using SkiaSharp;
using System.Numerics;

namespace Axolotl2D.Rendering;

internal sealed class SdfGlyphAtlas
{
    private const int AtlasSize = 1024;
    private const float RasterSize = 48f;
    private const int Spread = 8;
    private readonly FontAsset fontAsset;
    private readonly byte[] pixels = new byte[AtlasSize * AtlasSize * 4];
    private readonly Dictionary<char, Glyph> glyphs = [];
    private int nextX;
    private int nextY;
    private int rowHeight;

    public SdfGlyphAtlas(FontAsset fontAsset)
    {
        this.fontAsset = fontAsset;
        Texture = new(AtlasSize, AtlasSize, pixels);
    }

    private Texture2D Texture { get; }

    public Vector2 Measure(string text, float size)
    {
        var scale = size / RasterSize;
        var lineHeight = GetGlyph('M').LineHeight * scale;
        var x = 0f;
        var width = 0f;
        var lines = 1;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\r') continue;
            if (character == '\n') { width = MathF.Max(width, x); x = 0f; lines++; continue; }
            x += GetGlyph(character == '\t' ? ' ' : character).Advance * scale * (character == '\t' ? 4f : 1f);
        }
        return new(MathF.Max(width, x), lineHeight * lines);
    }

    public void Draw(SpriteBatch batch, string text, float size, Vector2 position, Color color,
        CoordinateSpace space, float depth)
    {
        var scale = size / RasterSize;
        var cursor = Vector2.Zero;
        var lineHeight = GetGlyph('M').LineHeight * scale;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\r') continue;
            if (character == '\n') { cursor.X = 0f; cursor.Y += lineHeight; continue; }
            if (character == '\t') { cursor.X += GetGlyph(' ').Advance * scale * 4f; continue; }
            var glyph = GetGlyph(character);
            if (glyph.Sprite is not null)
                batch.Draw(glyph.Sprite, position + cursor + glyph.Offset * scale,
                    glyph.Sprite.Size * scale, tint: color, space: space, depth: depth);
            cursor.X += glyph.Advance * scale;
        }
    }

    private Glyph GetGlyph(char character)
    {
        if (glyphs.TryGetValue(character, out var cached)) return cached;
        using var font = new SKFont(fontAsset.Typeface, RasterSize);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        font.GetFontMetrics(out var metrics);
        var text = character.ToString();
        var advance = font.MeasureText(text, out var bounds, paint);
        var lineHeight = MathF.Max(1f, metrics.Descent - metrics.Ascent + metrics.Leading);
        if (character == ' ')
            return glyphs[character] = new(null, advance, Vector2.Zero, lineHeight);

        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width) + Spread * 2);
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height) + Spread * 2);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawText(text, Spread - bounds.Left, Spread - bounds.Top, SKTextAlign.Left, font, paint);
            canvas.Flush();
        }

        var region = Allocate(width, height);
        WriteDistanceField(bitmap.GetPixelSpan().AsSpan2D(0, height, width, bitmap.RowBytes - width), region);
        Texture.MarkDirty(region);
        var sprite = new Sprite(Texture, region) { Origin = Vector2.Zero, IsSdf = true };
        var offset = new Vector2(bounds.Left - Spread, -metrics.Ascent + bounds.Top - Spread);
        return glyphs[character] = new(sprite, advance, offset, lineHeight);
    }

    private TextureRegion Allocate(int width, int height)
    {
        if (nextX + width > AtlasSize)
        {
            nextX = 0;
            nextY += rowHeight;
            rowHeight = 0;
        }
        if (nextY + height > AtlasSize)
            throw new InvalidOperationException("The SDF glyph atlas is full for this font.");
        var region = new TextureRegion(nextX, nextY, width, height);
        nextX += width;
        rowHeight = Math.Max(rowHeight, height);
        return region;
    }

    private void WriteDistanceField(ReadOnlySpan2D<byte> source, TextureRegion region)
    {
        var destination = pixels.AsSpan().AsSpan2D(AtlasSize, AtlasSize * 4);
        // Glyphs are rasterized once and Spread bounds this simple search to 17x17 pixels.
        for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var inside = source[y, x] >= 128;
                var nearestSquared = Spread * Spread;
                for (var offsetY = -Spread; offsetY <= Spread; offsetY++)
                    for (var offsetX = -Spread; offsetX <= Spread; offsetX++)
                    {
                        var sampleX = x + offsetX;
                        var sampleY = y + offsetY;
                        var sampleInside = sampleX >= 0 && sampleY >= 0 && sampleX < source.Width &&
                            sampleY < source.Height && source[sampleY, sampleX] >= 128;
                        if (sampleInside == inside) continue;
                        nearestSquared = Math.Min(nearestSquared, offsetX * offsetX + offsetY * offsetY);
                    }
                var distance = MathF.Sqrt(nearestSquared) / Spread;
                var value = (byte)Math.Clamp((int)MathF.Round((0.5f + (inside ? distance : -distance) * 0.5f) * 255f), 0, 255);
                var row = destination.GetRowSpan(region.Y + y);
                var pixel = (region.X + x) * 4;
                row[pixel] = 255; row[pixel + 1] = 255; row[pixel + 2] = 255; row[pixel + 3] = value;
            }
    }

    private readonly record struct Glyph(Sprite? Sprite, float Advance, Vector2 Offset, float LineHeight);
}
