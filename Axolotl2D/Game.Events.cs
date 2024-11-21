using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D
{
    public abstract partial class Game
    {
        public delegate void DrawDelegate(double frameDelta, double frameRate);
        public delegate void UpdateDelegate(double frameDelta);
        public delegate void LoadDelegate();
        public delegate void ResizeDelegate(Vector2D<int> size);

        public event DrawDelegate? OnDraw;
        public event UpdateDelegate? OnUpdate;
        public event LoadDelegate? OnLoad;
        public event ResizeDelegate? OnResize;
    }
}
