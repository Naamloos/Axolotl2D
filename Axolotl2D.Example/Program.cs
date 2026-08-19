using Axolotl2D.Example.Scenes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axolotl2D.Example;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                // The game-host registration also installs Axolotl2D's DI services.
                services.UseSceneManagerGameHost<ExampleGame>(true);
                services.AddScene<ExampleScene>();
                services.AddScene<ExampleScene2>();
            })
            .Build();

        await host.RunAsync();
    }
}
