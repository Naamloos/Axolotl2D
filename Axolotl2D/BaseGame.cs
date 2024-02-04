using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Axolotl2D
{
    public abstract class BaseGame : IDisposable
    {
        public double CurrentFramerate => _currentFramerate;
        private double _currentFramerate = 0;

        private readonly IWindow _window;
        private GL? _gl;
        private int _width;
        private int _height;
        private string _title;

        public BaseGame(string title, int width, int height)
        {
            _width = width;
            _height = height;
            _title = title;

            var options = WindowOptions.Default;
            options.Title = title;
            options.Size = new Vector2D<int>(_width, _height);
            options.WindowClass = "axl2d";
            options.WindowBorder = WindowBorder.Resizable;

            _window = Window.Create(options);

            // Hook window events
            _window.Load += _onLoad;
            _window.Render += _onDraw;
            _window.FramebufferResize += _onResize;
        }

        public void Start()
        {
            _window.Run();
        }

        public abstract void OnLoad();
        private void _onLoad()
        {
            // Prepare OpenGL context on load.
            _gl = _window.CreateOpenGL();
            _gl.ClearColor(0.4f, 0.6f, 1.0f, 1.0f);
            
            OnLoad();
        }

        public abstract void OnResize();
        private void _onResize(Vector2D<int> size)
        {
            if (_gl is null)
                return;

            // Handle resizes in GL context when window resizes.
            _gl.Viewport(size);
            _width = size.X;
            _height = size.Y;

            OnResize();
        }

        public abstract void OnDraw(double frameDelta, double frameRate);
        private void _onDraw(double delta)
        {
            if(_gl is null)
                return;

            _currentFramerate = 1.0f / delta;

            _gl.Clear(ClearBufferMask.ColorBufferBit);

            OnDraw(delta, _currentFramerate);

            _window.Title = $"{_title} | FPS: {Math.Round(_currentFramerate)}";
        }

        public void Dispose()
        {
            _window.Dispose();
        }

        ~BaseGame() => Dispose();
    }
}