using Axolotl2D.Assets;
using SkiaSharp;
using System.Numerics;

namespace Axolotl2D.Rendering;

/// <summary>Shapes scalable font text and caches its transparent texture.</summary>
public sealed class TextRenderer
{
    private readonly Dictionary<(FontAsset Font, float Size, string Text), Texture2D> cache = [];

    public Texture2D Render(FontAsset fontAsset, string text, float fontSize)
    {
        ArgumentNullException.ThrowIfNull(fontAsset);
        ArgumentNullException.ThrowIfNull(text);
        if (fontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));

        var key = (fontAsset, fontSize, text);
        if (cache.TryGetValue(key, out var cached))
            return cached;

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

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        for (var i = 0; i < lines.Length; i++)
            canvas.DrawText(lines[i], -left, i * lineHeight - metrics.Ascent, SKTextAlign.Left, font, paint);
        canvas.Flush();

        var alpha = bitmap.GetPixelSpan();
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var sourceRow = alpha.Slice(y * bitmap.RowBytes, width);
            for (var x = 0; x < width; x++)
            {
                var pixel = (y * width + x) * 4;
                pixels[pixel] = 255;
                pixels[pixel + 1] = 255;
                pixels[pixel + 2] = 255;
                pixels[pixel + 3] = sourceRow[x];
            }
        }

        var texture = new Texture2D(width, height, pixels);
        cache.Add(key, texture);
        return texture;
    }

    public void Draw(SpriteBatch spriteBatch, FontAsset font, string text, float fontSize, Vector2 position,
        Color? color = null, CoordinateSpace space = CoordinateSpace.Screen, float depth = 0f)
    {
        var texture = Render(font, text, fontSize);
        var sprite = new Sprite(texture) { Origin = Vector2.Zero };
        spriteBatch.Draw(sprite, position, tint: color, space: space, depth: depth);
    }
}
