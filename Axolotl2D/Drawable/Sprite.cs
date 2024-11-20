using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Drawable
{
    public class Sprite : IDrawable
    {
        private GL _glContext;
        private uint _handle;

        public static Sprite Load(Game game, Stream image)
        {
            return new Sprite(game, image);
        }

        internal unsafe Sprite(Game game, Stream imageStream)
        {
            _glContext = game.GetOpenGLContext();
            _handle = _glContext.GenTexture();
            _glContext.ActiveTexture(TextureUnit.Texture0);
            _glContext.BindTexture(TextureTarget.Texture2D, _handle);

            using(var image = Image.Load<Rgba32>(imageStream))
            {
                _glContext.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 
                    (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

                image.ProcessPixelRows(accessor =>
                {
                    for(int y = 0; y < accessor.Height; y++)
                    {
                        fixed(void* data = accessor.GetRowSpan(y))
                        {
                            _glContext.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                        }
                    }
                });
            }
        }

        public void Draw()
        {
        }
    }
}
