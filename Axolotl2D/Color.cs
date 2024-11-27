namespace Axolotl2D
{
    /// <summary>
    /// Represents a color in the game.
    /// </summary>
    public class Color
    {
        /// <summary>
        /// The red component of the color.
        /// </summary>
        public float R { get; }

        /// <summary>
        /// The green component of the color.
        /// </summary>
        public float G { get; }

        /// <summary>
        /// The blue component of the color.
        /// </summary>
        public float B { get; }

        /// <summary>
        /// The alpha component of the color.
        /// </summary>
        public float A { get; }

        /// <summary>
        /// The numeric value of the color.
        /// </summary>
        public uint Value => (uint)((int)(A * 255) << 24 | (int)(R * 255) << 16 | (int)(G * 255) << 8 | (int)(B * 255));

        /// <summary>
        /// Initialize a new Color object.
        /// </summary>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// <param name="a">Alpha</param>
        public Color(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>
        /// Create a new color from RGB values.
        /// </summary>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// <returns>The new Color</returns>
        public static Color FromRGB(float r, float g, float b)
        {
            return new Color(r, g, b, 1.0f);
        }

        /// <summary>
        /// Create a new color from a HTML color string.
        /// </summary>
        /// <param name="html">The HTML color to parse</param>
        /// <returns>The new color</returns>
        /// <exception cref="ArgumentException"></exception>
        public static Color FromHTML(string html)
        {
            if (html.StartsWith("#"))
            {
                html = html.Substring(1);
            }
            if (html.Length != 6)
            {
                throw new ArgumentException("HTML color must be 6 characters long");
            }
            var r = Convert.ToInt32(html.Substring(0, 2), 16) / 255.0f;
            var g = Convert.ToInt32(html.Substring(2, 2), 16) / 255.0f;
            var b = Convert.ToInt32(html.Substring(4, 2), 16) / 255.0f;
            return new Color(r, g, b, 1.0f);
        }

        /// <summary>
        /// #FF0000
        /// </summary>
        public static Color Red => new Color(1.0f, 0.0f, 0.0f, 1.0f);

        /// <summary>
        /// #00FF00
        /// </summary>
        public static Color Green => new Color(0.0f, 1.0f, 0.0f, 1.0f);

        /// <summary>
        /// #0000FF
        /// </summary>
        public static Color Blue => new Color(0.0f, 0.0f, 1.0f, 1.0f);

        /// <summary>
        /// #000000
        /// </summary>
        public static Color Black => new Color(0.0f, 0.0f, 0.0f, 1.0f);

        /// <summary>
        /// #FFFFFF
        /// </summary>
        public static Color White => new Color(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// #FFFF00
        /// </summary>
        public static Color Yellow => new Color(1.0f, 1.0f, 0.0f, 1.0f);

        /// <summary>
        /// #FF00FF
        /// </summary>
        public static Color Cyan => new Color(0.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// #FF00FF
        /// </summary>
        public static Color Magenta => new Color(1.0f, 0.0f, 1.0f, 1.0f);

        /// <summary>
        /// #00000000
        /// </summary>
        public static Color Transparent => new Color(0.0f, 0.0f, 0.0f, 0.0f);

        /// <summary>
        /// #808080
        /// </summary>
        public static Color Gray => new Color(0.5f, 0.5f, 0.5f, 1.0f);

        /// <summary>
        /// #404040
        /// </summary>
        public static Color DarkGray => new Color(0.25f, 0.25f, 0.25f, 1.0f);

        /// <summary>
        /// #C0C0C0
        /// </summary>
        public static Color LightGray => new Color(0.75f, 0.75f, 0.75f, 1.0f);

        /// <summary>
        /// #FFA500
        /// </summary>
        public static Color Orange => new Color(1.0f, 0.647f, 0.0f, 1.0f);

        /// <summary>
        /// #A52A2A
        /// </summary>
        public static Color Brown => new Color(0.647f, 0.164f, 0.164f, 1.0f);
        // some funnies
        /// <summary>
        /// #0023FF
        /// </summary>
        public static Color RamptoerismeBlue => FromHTML("#0023FF");
    }
}
