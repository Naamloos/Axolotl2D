using Axolotl2D.Interfaces;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Sample.Scenes
{
    public class MainScene : IScene
    {
        public async Task InitializeAsync()
        {
            await Task.Yield();
        }

        public async Task OnDrawAsync(GameTime gameTime)
        {
            await Task.Yield();
        }

        public async Task OnUpdateAsync(GameTime gameTime)
        {
            await Task.Yield();
        }

        public async Task OnFixedUpdateAsync(GameTime gameTime)
        {
            await Task.Yield();
        }

        public async Task DestroyAsync()
        {
            await Task.Yield();
        }
    }
}
