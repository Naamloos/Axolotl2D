using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Interfaces
{
    public interface IScene
    {
        Task InitializeAsync();

        Task OnUpdateAsync(GameTime gameTime);

        Task OnFixedUpdateAsync(GameTime gameTime);

        Task OnDrawAsync(GameTime gameTime);

        Task DestroyAsync();
    }
}
