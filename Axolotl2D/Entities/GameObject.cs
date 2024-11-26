using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    public abstract class GameObject
    {
        public virtual void Update() { }

        public virtual void Draw() { }

        public virtual void Dispose() { }
    }
}
