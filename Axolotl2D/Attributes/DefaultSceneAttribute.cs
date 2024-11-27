using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Attributes
{
    /// <summary>
    /// To be applied to a Scene class to mark it as the default scene.
    /// This scene will be loaded when the Scene Manager is initialized.
    /// </summary>
    public class DefaultSceneAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of the DefaultSceneAttribute.
        /// </summary>
        public DefaultSceneAttribute()
        {
        }
    }
}
