using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using CefSharp.OffScreen;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Numerics;
using System.Reflection;

// https://github.com/cefsharp/CefSharp/wiki/General-Usage#need-to-knowlimitations
// https://github.com/cefsharp/CefSharp/issues/1714

namespace Axolotl2D.Cef
{
    // Requires setting a runtime identifier
    // <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
    public class CefBrowser : BaseDrawable
    {
        private readonly GL _gl;
        private readonly ChromiumWebBrowser _browser;
        private uint _vbo;
        private uint _ebo;
        private uint _vao;
        private uint _texture;
        private nint _lastBufferHandle;

        private static bool _cefInitialized = false;

        public CefBrowser(Game game, Vector2 position, Vector2 size) : base(game, position, size)
        {
            if (!_cefInitialized)
            {
                var cefSett = new CefSettings
                {
                    RootCachePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty, "cefCache"),
                    WindowlessRenderingEnabled = true,
                    LogSeverity = CefSharp.LogSeverity.Verbose,
                    LogFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty, "cef.log")
                };
                CefSharp.Cef.Initialize(cefSett, true, browserProcessHandler: null);
                _cefInitialized = true;
            }

            _gl = game._openGL ?? throw new ArgumentNullException(nameof(game));
            _browser = new ChromiumWebBrowser("https://github.com/Naamloos/Axolotl2D")
            {
                Size = new System.Drawing.Size((int)size.X, (int)size.Y)
            };
            InitializeBuffers();
            InitializeTexture();
            _browser.Paint += OnBrowserPaint;
        }

        private unsafe void InitializeBuffers()
        {
            // Create a VAO.
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            // Create a VBO.
            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // fix vertices and buffer data
            fixed (void* vertices = _vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_vertices.Length * sizeof(float)), vertices, BufferUsageARB.StaticDraw);

            // Create an EBO.
            _ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            fixed (void* indices = _indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(_indices.Length * sizeof(uint)), indices, BufferUsageARB.StaticDraw);

            const uint positionLocation = 0;
            _gl.EnableVertexAttribArray(positionLocation);
            _gl.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            const uint texCoordLocation = 1;
            _gl.EnableVertexAttribArray(texCoordLocation);
            _gl.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        }

        private unsafe void InitializeTexture()
        {
            _texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Size.X, (uint)Size.Y, 0, PixelFormat.Bgra, PixelType.UnsignedByte, null);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void OnBrowserPaint(object? sender, OnPaintEventArgs e)
        {
            _lastBufferHandle = e.BufferHandle;
        }

        public unsafe override void Draw()
        {
            if (_lastBufferHandle != nint.Zero)
            {
                _gl.BindTexture(TextureTarget.Texture2D, _texture);

                byte* ptr = (byte*)_lastBufferHandle;
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)Size.X, (uint)Size.Y, PixelFormat.Bgra, PixelType.UnsignedByte, ptr);

                int location = _gl.GetUniformLocation(_game._shaderProgram, "uTexture");
                _gl.Uniform1(location, 0);

                _gl.BindTexture(TextureTarget.Texture2D, 0);

                _lastBufferHandle = nint.Zero;
            }

            _gl.UseProgram(_game._shaderProgram);

            _gl.BindVertexArray(_vao);

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _texture);

            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

            _gl.BindVertexArray(0);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _gl.UseProgram(0);
        }

        public override void Draw(Vector2 position)
        {
            Position = position;
            Draw();
        }

        public override void Draw(Vector2 position, Vector2 size)
        {
            Bounds = (position, size);
            Draw();
        }
    }
}
