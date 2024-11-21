using Microsoft.Extensions.Hosting;
using Axolotl2D;
using Microsoft.Extensions.DependencyInjection;

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
                    services.AddLogging();
                })
                .Build();

            host.Start();
        }
    }
}