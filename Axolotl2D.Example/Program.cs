using Microsoft.Extensions.Hosting;
using Axolotl2D;

namespace Axolotl2D.Example
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddGame<ExampleGame>();
                })
                .Build();

            host.Start();
        }
    }
}