using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    /// <summary>
    /// WIP: Represents a game object in the game.
    /// </summary>
    public abstract class GameObject
    {
        /// <summary>
        /// Update this GameObject.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// Draw this GameObject.
        /// </summary>
        public virtual void Draw() { }

        /// <summary>
        /// Dispose of this GameObject.
        /// </summary>
        public virtual void Dispose() { }
    }
}
