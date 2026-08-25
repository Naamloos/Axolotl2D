using Axolotl2D.Example.Stress.Scenes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axolotl2D.Example.Stress;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Contains("--check", StringComparer.Ordinal))
        {
            StressScene.RunSelfCheck();
            Console.WriteLine("Stress workload check passed.");
            return;
        }

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.UseSceneManagerGameHost<StressGame>(enableDebugOverlay: true);
                services.AddScene<StressScene>();
            })
            .Build();

        await host.RunAsync();
    }
}
