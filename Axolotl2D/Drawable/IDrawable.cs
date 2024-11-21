using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Drawable
{
    public interface IDrawable
    {
        public void Draw();

        public void Draw(float x, float y, float width, float height);
    }
}
