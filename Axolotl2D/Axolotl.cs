using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Axolotl2D
{
    public abstract class Axolotl : IDisposable
    {
        public double CurrentFramerate
        {
            get => _currentFramerate;
        }
        private double _currentFramerate = 0;

        private readonly IWindow _window;
        private GL? _gl;
        private ImGuiController? _imgui;
        private int _width;
        private int _height;
        private readonly bool _debug;

        public Axolotl(string title, int width, int height, bool debug = false)
        {
            _width = width;
            _height = height;
            _debug = debug;

            var options = WindowOptions.Default;
            options.Title = title;
            options.Size = new Vector2D<int>(width, height);
            options.WindowClass = "axolotl2d";
            options.WindowBorder = WindowBorder.Resizable;

            _window = Window.Create(options);

            // Hook window events
            _window.Load += onWindowLoad;
            _window.Render += onWindowRender;
            _window.FramebufferResize += onWindowResize;
        }

        public void Start()
        {
            _window.Run();
        }

        public abstract void OnLoad();
        private void onWindowLoad()
        {
            // Prepare OpenGL context on load.
            _gl = _window.CreateOpenGL();
            _gl.ClearColor(0.4f, 0.6f, 1.0f, 1.0f);
            _imgui = new ImGuiController(_gl, _window, _window.CreateInput());
            OnLoad();
        }

        public abstract void OnResize();
        private void onWindowResize(Vector2D<int> size)
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
        private void onWindowRender(double delta)
        {
            if(_gl is null)
                return;

            _currentFramerate = 1.0f / delta;

            _gl.Clear(ClearBufferMask.ColorBufferBit);

            if (_debug)
                renderDebugMenu(delta);

            OnDraw(delta, _currentFramerate);
        }

        private void renderDebugMenu(double delta)
        {
            if (_imgui is null)
                return;

            _imgui.Update((float)delta);
            ImGuiNET.ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
            ImGuiNET.ImGui.SetWindowSize(new System.Numerics.Vector2(_width / 3, _height));
            ImGuiNET.ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.4f, 0.6f, 1.0f), "Axolotl2D debug menu");
            ImGuiNET.ImGui.Text($"Framerate: {_currentFramerate}");

            if (ImGuiNET.ImGui.Button("force throw exception"))
            {
                throw new Exception("Forced exception from engine debugger");
            }

            _imgui.Render();
        }

        public void Dispose()
        {
            _window.Dispose();
        }

        ~Axolotl() => Dispose();
    }
}