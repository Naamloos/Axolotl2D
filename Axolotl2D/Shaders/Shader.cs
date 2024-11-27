using Axolotl2D.Exceptions;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Shaders
{
    /// <summary>
    /// Represents a shader in the game.
    /// </summary>
    public class Shader
    {
        private string _source;
        private GL _gl;
        private uint _shaderPointer;
        private bool compiled = false;
        private ShaderType _shaderType;
        private Game _game;

        /// <summary>
        /// Initialize a new Shader object.
        /// </summary>
        /// <param name="shaderSource">GLSL code for this shader</param>
        /// <param name="shaderType">Type of this shader</param>
        /// <param name="game">Game to initialize shader on</param>
        public Shader(string shaderSource, ShaderType shaderType, Game game)
        {
            _source = shaderSource;
            _gl = game._openGL!;
            _shaderType = shaderType;
            _game = game;
        }

        /// <summary>
        /// Compile the shader.
        /// </summary>
        /// <exception cref="ShaderCompileException">Shader failed to compile</exception>
        public void Compile()
        {
            uint shaderPointer = _gl.CreateShader(_shaderType);
            _gl.ShaderSource(shaderPointer, _source);
            _gl.CompileShader(shaderPointer);

            _gl.GetShader(shaderPointer, ShaderParameterName.CompileStatus, out int vertexShaderStatus);
            if (vertexShaderStatus != (int)GLEnum.True)
                throw new ShaderCompileException("Shader failed to compile: " + _gl.GetShaderInfoLog(shaderPointer), this);

            compiled = true;
            _shaderPointer = shaderPointer;
        }

        /// <summary>
        /// Attach the shader to the program.
        /// </summary>
        /// <exception cref="ShaderNotCompiledException">Shader was not compiled yet</exception>
        public void AttachToProgram()
        {
            if (!compiled)
                throw new ShaderNotCompiledException("Tried attaching a shader to a program while it was not compiled yet!", this);
            _gl.AttachShader(_game._shaderProgram, _shaderPointer);
        }

        /// <summary>
        /// Detach the shader from the program.
        /// </summary>
        /// <exception cref="ShaderNotCompiledException">Shader was not compiled yet</exception>
        public void DetachFromProgram()
        {
            if (!compiled)
                throw new ShaderNotCompiledException("Tried detaching a shader from a program while it was not compiled yet!", this);
            _gl.DetachShader(_game._shaderProgram, _shaderPointer);
            _gl.DeleteShader(_shaderPointer);
            compiled = false;
        }

        /// <summary>
        /// Get the pointer to the shader.
        /// </summary>
        /// <returns>Pointer to the shader</returns>
        /// <exception cref="ShaderNotCompiledException">Shader was not compiled yet</exception>
        public uint GetPointer() => compiled ? _shaderPointer : throw new ShaderNotCompiledException("Tried accessing the shader pointer to a shader that was not compiled yet!", this);

        /// <summary>
        /// Create a basic fragment shader.
        /// </summary>
        /// <param name="game">Game to create the shader on.</param>
        /// <returns>A basic fragment shader</returns>
        public static Shader CreateBasicFragment(Game game)
        {
            // load from embedded resources as text
            using var shader = typeof(Shader).Assembly.GetManifestResourceStream("Axolotl2D.Shaders.BasicFragment.glsl");
            using var reader = new StreamReader(shader!);
            return new Shader(reader.ReadToEnd(), ShaderType.FragmentShader, game);
        }

        /// <summary>
        /// Create a basic vertex shader.
        /// </summary>
        /// <param name="game">Game to create the shader on.</param>
        /// <returns>A basic vertex shader</returns>
        public static Shader CreateBasicVertex(Game game)
        {
            // load from embedded resources as text
            using var shader = typeof(Shader).Assembly.GetManifestResourceStream("Axolotl2D.Shaders.BasicVertex.glsl");
            using var reader = new StreamReader(shader!);
            return new Shader(reader.ReadToEnd(), ShaderType.VertexShader, game);
        }
    }
}
