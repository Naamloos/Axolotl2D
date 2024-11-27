using Axolotl2D.Shaders;

namespace Axolotl2D.Exceptions
{
    /// <summary>
    /// Thrown when an action is performed that required a shader to be compiled, but it is not.
    /// </summary>
    public class ShaderNotCompiledException : Exception
    {
        /// <summary>
        /// The shader that failed to compile.
        /// </summary>
        public Shader Shader { get; }

        internal ShaderNotCompiledException(string message, Shader shader) : base(message)
        {
            Shader = shader;
        }
    }
}
