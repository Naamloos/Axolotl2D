namespace Axolotl2D.Example
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using(var game = new MyGame())
            {
                game.Start();
            }
        }
    }
}