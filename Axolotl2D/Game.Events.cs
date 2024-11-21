using System.Numerics;

namespace Axolotl2D
{
    public abstract partial class Game
    {
        public delegate void DrawDelegate(double frameDelta, double frameRate);
        public delegate void UpdateDelegate(double frameDelta);
        public delegate void LoadDelegate();
        public delegate void ResizeDelegate(Vector2 size);

        public event DrawDelegate? OnDraw;
        public event UpdateDelegate? OnUpdate;
        public event LoadDelegate? OnLoad;
        public event ResizeDelegate? OnResize;
    }
}
