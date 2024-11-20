using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    public class Point
    {
        private int _x;
        private int _y;
        private Game _game;

        public Point(int x, int y, Game game) 
        {
            _x = x;
            _y = y;
            _game = game;
        }

        public float[] GetValue()
        {
            return [
                (float)_x / _game.GetWidth() * 2 - 1,
                (float)_y / _game.GetHeight() * 2 - 1,
                0
            ];
        }
    }
}
