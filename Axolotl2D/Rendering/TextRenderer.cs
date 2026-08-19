using Axolotl2D.Assets;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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

        var font = fontAsset.Family.CreateFont(fontSize);
        var content = text.Length == 0 ? " " : text;
        var bounds = TextMeasurer.MeasureRenderableBounds(content, new TextOptions(font));
        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        using var image = new Image<Rgba32>(width, height);
        var options = new RichTextOptions(font) { Origin = new PointF(-bounds.Left, -bounds.Top) };
        image.Mutate(context => context.Paint(canvas =>
            canvas.DrawText(options, text, Brushes.Solid(SixLabors.ImageSharp.Color.White), pen: null)));

        var pixels = new byte[width * height * 4];
        image.CopyPixelDataTo(pixels);
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
