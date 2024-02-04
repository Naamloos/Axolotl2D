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
    public class Sprite : ISprite
    {
        private GL _glContext;
        private uint _handle;

        internal unsafe Sprite(GL gl, Stream imageStream)
        {
            _glContext = gl;
            _handle = gl.GenTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, _handle);

            using(var image = Image.Load<Rgba32>(imageStream))
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 
                    (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);

                image.ProcessPixelRows(accessor =>
                {
                    for(int y = 0; y < accessor.Height; y++)
                    {
                        fixed(void* data = accessor.GetRowSpan(y))
                        {
                            gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                        }
                    }
                });
            }
        }
    }
}
