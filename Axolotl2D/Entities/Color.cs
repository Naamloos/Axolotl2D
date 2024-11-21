using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    public class Color
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public Color(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Color FromRGB(float r, float g, float b)
        {
            return new Color(r, g, b, 1.0f);
        }

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

        public static Color Red => new Color(1.0f, 0.0f, 0.0f, 1.0f);
        public static Color Green => new Color(0.0f, 1.0f, 0.0f, 1.0f);
        public static Color Blue => new Color(0.0f, 0.0f, 1.0f, 1.0f);
        public static Color Black => new Color(0.0f, 0.0f, 0.0f, 1.0f);
        public static Color White => new Color(1.0f, 1.0f, 1.0f, 1.0f);
        public static Color Yellow => new Color(1.0f, 1.0f, 0.0f, 1.0f);
        public static Color Cyan => new Color(0.0f, 1.0f, 1.0f, 1.0f);
        public static Color Magenta => new Color(1.0f, 0.0f, 1.0f, 1.0f);
        public static Color Transparent => new Color(0.0f, 0.0f, 0.0f, 0.0f);
        public static Color Gray => new Color(0.5f, 0.5f, 0.5f, 1.0f);
        public static Color DarkGray => new Color(0.25f, 0.25f, 0.25f, 1.0f);
        public static Color LightGray => new Color(0.75f, 0.75f, 0.75f, 1.0f);
        public static Color Orange => new Color(1.0f, 0.647f, 0.0f, 1.0f);
        public static Color Brown => new Color(0.647f, 0.164f, 0.164f, 1.0f);
        // some funnies
        public static Color RamptoerismeBlue => Color.FromHTML("#0023FF");
    }
}
