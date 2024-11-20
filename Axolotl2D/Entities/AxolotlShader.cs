﻿using Axolotl2D.Exceptions;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Entities
{
    public class AxolotlShader
    {
        private string _source;
        private GL _gl;
        private uint _shaderPointer;
        private bool compiled = false;
        private ShaderType _shaderType;
        private Game _game;

        public AxolotlShader(string shaderSource, ShaderType shaderType, Game game)
        {
            _source = shaderSource;
            _gl = game.GetOpenGLContext();
            _shaderType = shaderType;
            _game = game;
        }

        public void Compile()
        {
            uint shaderPointer = _gl.CreateShader(_shaderType);
            _gl.ShaderSource(shaderPointer, _source);
            _gl.CompileShader(shaderPointer);

            _gl.GetShader(shaderPointer, ShaderParameterName.CompileStatus, out int vertexShaderStatus);
            if (vertexShaderStatus != (int)GLEnum.True)
                throw new ShaderCompileException("Vertex shader failed to compile: " + _gl.GetShaderInfoLog(shaderPointer), this);

            compiled = true;
            _shaderPointer = shaderPointer;
        }

        public void AttachToProgram()
        {
            if (!compiled)
                throw new ShaderNotCompiledException("Tried attaching a shader to a program while it was not compiled yet!", this);
            _gl.AttachShader(_game.GetShaderProgram(), _shaderPointer);
        }

        public void DetachFromProgram()
        {
            if (!compiled)
                throw new ShaderNotCompiledException("Tried detaching a shader from a program while it was not compiled yet!", this);
            _gl.DetachShader(_game.GetShaderProgram(), _shaderPointer);
            _gl.DeleteShader(_shaderPointer);
            compiled = false;
        }

        public uint GetPointer() => compiled? _shaderPointer : throw new ShaderNotCompiledException("Tried accessing the shader pointer to a shader that was not compiled yet!", this);

        public static AxolotlShader CreateBasicFragment(Game game)
        {
            // load from embedded resources as text
            using var shader = typeof(AxolotlShader).Assembly.GetManifestResourceStream("Axolotl2D.Shaders.BasicFragment.glsl");
            using var reader = new StreamReader(shader!);
            return new AxolotlShader(reader.ReadToEnd(), ShaderType.FragmentShader, game);
        }

        public static AxolotlShader CreateBasicVertex(Game game)
        {
            // load from embedded resources as text
            using var shader = typeof(AxolotlShader).Assembly.GetManifestResourceStream("Axolotl2D.Shaders.BasicVertex.glsl");
            using var reader = new StreamReader(shader!);
            return new AxolotlShader(reader.ReadToEnd(), ShaderType.VertexShader, game);
        }
    }
}
