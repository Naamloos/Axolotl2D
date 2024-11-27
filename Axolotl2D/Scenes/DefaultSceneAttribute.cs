namespace Axolotl2D.Scenes
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
