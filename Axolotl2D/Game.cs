using Axolotl2D.Entities;
using Axolotl2D.Exceptions;
using Axolotl2D.Input;
using Microsoft.Extensions.Hosting;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Axolotl2D
{
    public abstract partial class Game : IDisposable
    {
        /// <summary>
        /// Represents the window title for the game.
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Represents the window width for the game.
        /// </summary>
        public int WindowWidth
        {
            get => _width;
            set
            {
                _width = value;
                _window.Size = new Vector2D<int>(_width, _height);
            }
        }
        private int _width;

        /// <summary>
        /// Represents the window height for the game.
        /// </summary>
        public int WindowHeight
        {
            get => _height;
            set
            {
                _height = value;
                _window.Size = new Vector2D<int>(_width, _height);
            }
        }
        private int _height;

        /// <summary>
        /// Represents the current framerate of the game.
        /// </summary>
        public double CurrentFramerate { get; private set; }

        /// <summary>
        /// Represents the number of loaded sprites in the game.
        /// </summary>
        public int LoadedSprites { get; internal set; }

        /// <summary>
        /// Represents the clear color of the game.
        /// </summary>
        public Color ClearColor
        {
            get => _clearColor;
            set
            {
                _clearColor = value;
                if (_openGL is not null)
                    _openGL.ClearColor(_clearColor.R, _clearColor.G, _clearColor.B, _clearColor.A);
            }
        }

        private Color _clearColor = Color.Cyan;

        internal GL? _openGL;
        internal readonly IWindow _window;
        internal IInputContext? _input;

        private Entities.Shader? _basicVertexShader;
        private Entities.Shader? _basicFragmentShader;

        internal uint _shaderProgram;

        internal IServiceProvider _services;

        public Game(IServiceProvider services, int maxDrawRate = 120, int maxUpdateRate = 120) // TODO make configurable at runtime
        {
            _services = services;

            _width = 500;
            _height = 500;

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(_width, _height);
            options.WindowClass = "axl2d";
            options.WindowBorder = WindowBorder.Resizable;
            options.FramesPerSecond = maxDrawRate;
            options.VSync = false;
            options.UpdatesPerSecond = maxUpdateRate;

            _window = Window.Create(options);

            // Hook window events
            _window.Load += _onLoad;
            _window.Render += _onDraw;
            _window.FramebufferResize += _onResize;
            _window.Update += _onUpdate;
        }

        internal void start()
        {
            _window.Run();
        }

        internal void stop()
        {
            _window.Close();
        }

        private int _gcCounter = 0;
        private void _onUpdate(double frameDelta)
        {
            if (_openGL is null)
                return;

            _gcCounter++;
            if (_gcCounter >= 2400)
            {
                GC.Collect();
                _gcCounter = 0;
            }

            OnUpdate?.Invoke(frameDelta);
        }

        /// <summary>
        /// Gets called when the game expects to load resources.
        /// </summary>
        private void _onLoad()
        {
            // Prepare OpenGL context on load.
            _openGL = _window.CreateOpenGL();

            _openGL.ClearColor(ClearColor.R, ClearColor.G, ClearColor.B, ClearColor.A);

            // Load basic shaders
            _basicVertexShader = Entities.Shader.CreateBasicVertex(this);
            _basicFragmentShader = Entities.Shader.CreateBasicFragment(this);

            // Compile basic shaders
            _basicVertexShader.Compile();
            _basicFragmentShader.Compile();

            // Create shader program
            _shaderProgram = _openGL.CreateProgram();

            // Attach basic shaders to program
            _basicVertexShader.AttachToProgram();
            _basicFragmentShader.AttachToProgram();

            _openGL.LinkProgram(_shaderProgram);

            _openGL.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
            if (lStatus != (int)GLEnum.True)
                throw new Exception("Program failed to link: " + _openGL.GetProgramInfoLog(_shaderProgram));

            _basicVertexShader.DetachFromProgram();
            _basicFragmentShader.DetachFromProgram();

            _openGL.Enable(EnableCap.Blend);
            _openGL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _input = _window.CreateInput();

            OnLoad?.Invoke();
        }

        private void _onResize(Vector2D<int> size)
        {
            if (_openGL is null)
                return;

            // Handle resizes in GL context when window resizes.
            _openGL.Viewport(size);
            _width = size.X;
            _height = size.Y;

            this.OnResize?.Invoke(new Vector2(size.X, size.Y));
        }

        private void _onDraw(double frameDelta)
        {
            if(_openGL is null)
                return;

            CurrentFramerate = Math.Ceiling(1.0f / frameDelta);

            _openGL.UseProgram(_shaderProgram);

            _openGL.Clear(ClearBufferMask.ColorBufferBit);

            OnDraw?.Invoke(frameDelta, CurrentFramerate);

            _window.Title = $"{Title} | FPS: {Math.Round(CurrentFramerate)}";
        }

        public abstract void Cleanup();

        /// <summary>
        /// Disposes the game.
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            _window.Dispose();
        }

        ~Game() => Dispose();
    }
}