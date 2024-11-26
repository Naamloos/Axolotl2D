using Axolotl2D.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example.Scenes
{
    public class ExampleScene2 : BaseScene
    {
        public ExampleScene2(ExampleGame game)
        {
            game.Title = "Example Scene 2";
            game.ClearColor = Color.FromHTML("#FF00FF");
        }
    }
}
