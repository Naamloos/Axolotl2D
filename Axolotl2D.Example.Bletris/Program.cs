using Axolotl2D.Example.Bletris.Scenes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axolotl2D.Example.Bletris;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Contains("--check", StringComparer.Ordinal))
        {
            BletrisBoard.RunSelfCheck();
            Console.WriteLine("Bletris rules check passed.");
            return;
        }

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.UseSceneManagerGameHost<BletrisGame>();
                services.AddScene<BletrisScene>();
            })
            .Build();

        await host.RunAsync();
    }
}
