using Axolotl2D.Rendering;
using SkiaSharp;
using System.Numerics;

namespace Axolotl2D.Debugging;

internal sealed class DebugGlyphAtlas
{
    private const int FirstCharacter = 32;
    private const int LastCharacter = 126;
    private const int Columns = 16;
    public const int CellWidth = 8;
    public const int CellHeight = 15;

    private readonly Sprite[] glyphs;

    public DebugGlyphAtlas()
    {
        var count = LastCharacter - FirstCharacter + 1;
        var rows = (count + Columns - 1) / Columns;
        var width = Columns * CellWidth;
        var height = rows * CellHeight;

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var typeface = SKTypeface.FromFamilyName("Consolas");
        using var font = new SKFont(typeface, 12f);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.Clear(SKColors.Transparent);

        for (var index = 0; index < count; index++)
        {
            var x = index % Columns * CellWidth;
            var y = index / Columns * CellHeight;
            canvas.DrawText(((char)(index + FirstCharacter)).ToString(), x, y + 12f,
                SKTextAlign.Left, font, paint);
        }
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
        glyphs = new Sprite[count];
        for (var index = 0; index < count; index++)
        {
            glyphs[index] = new Sprite(texture, new TextureRegion(
                index % Columns * CellWidth,
                index / Columns * CellHeight,
                CellWidth,
                CellHeight)) { Origin = Vector2.Zero };
        }
    }

    public void Draw(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float depth, float scale = 1f)
    {
        if (scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        var cursor = position;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                cursor.X = position.X;
                cursor.Y += CellHeight * scale;
                continue;
            }
            if (character == '\t')
            {
                cursor.X += CellWidth * 4 * scale;
                continue;
            }

            var printable = character is >= (char)FirstCharacter and <= (char)LastCharacter ? character : '?';
            spriteBatch.Draw(glyphs[printable - FirstCharacter], cursor,
                new Vector2(CellWidth, CellHeight) * scale,
                tint: color, space: CoordinateSpace.Screen, depth: depth);
            cursor.X += CellWidth * scale;
        }
    }
}
