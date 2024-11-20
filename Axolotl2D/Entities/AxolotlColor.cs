using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    public class AxolotlColor
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public AxolotlColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static AxolotlColor FromRGB(float r, float g, float b)
        {
            return new AxolotlColor(r, g, b, 1.0f);
        }

        public static AxolotlColor FromHTML(string html)
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
            return new AxolotlColor(r, g, b, 1.0f);
        }

        public static AxolotlColor Red => new AxolotlColor(1.0f, 0.0f, 0.0f, 1.0f);
        public static AxolotlColor Green => new AxolotlColor(0.0f, 1.0f, 0.0f, 1.0f);
        public static AxolotlColor Blue => new AxolotlColor(0.0f, 0.0f, 1.0f, 1.0f);
        public static AxolotlColor Black => new AxolotlColor(0.0f, 0.0f, 0.0f, 1.0f);
        public static AxolotlColor White => new AxolotlColor(1.0f, 1.0f, 1.0f, 1.0f);
        public static AxolotlColor Yellow => new AxolotlColor(1.0f, 1.0f, 0.0f, 1.0f);
        public static AxolotlColor Cyan => new AxolotlColor(0.0f, 1.0f, 1.0f, 1.0f);
        public static AxolotlColor Magenta => new AxolotlColor(1.0f, 0.0f, 1.0f, 1.0f);
        public static AxolotlColor Transparent => new AxolotlColor(0.0f, 0.0f, 0.0f, 0.0f);
        public static AxolotlColor Gray => new AxolotlColor(0.5f, 0.5f, 0.5f, 1.0f);
        public static AxolotlColor DarkGray => new AxolotlColor(0.25f, 0.25f, 0.25f, 1.0f);
        public static AxolotlColor LightGray => new AxolotlColor(0.75f, 0.75f, 0.75f, 1.0f);
        public static AxolotlColor Orange => new AxolotlColor(1.0f, 0.647f, 0.0f, 1.0f);
        public static AxolotlColor Brown => new AxolotlColor(0.647f, 0.164f, 0.164f, 1.0f);
        // some funnies
        public static AxolotlColor RamptoerismeBlue => AxolotlColor.FromHTML("#0023FF");
    }
}
