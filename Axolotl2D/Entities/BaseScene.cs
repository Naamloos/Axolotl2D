using Axolotl2D.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    public abstract class BaseScene
    {
        protected SceneGameHost SceneGameHost { get => _sceneGameHost!; }
        internal SceneGameHost? _sceneGameHost;

        public virtual void Draw(double frameDelta, double frameRate) { }
        public virtual void Update(double frameDelta) { }
        public virtual void Load() { }
        public virtual void Resize(Vector2 size) { }
        public virtual void Unload() { }
    }
}
