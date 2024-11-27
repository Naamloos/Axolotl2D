using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Axolotl2D.Example.Scenes;
using Axolotl2D.Cef;

namespace Axolotl2D.Example
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.UseSceneManagerGameHost<ExampleGame>();

                    services.UseAssetManager();

                    services.UseCefBrowserManager();

                    services.UseAudioPlayer();

                    services.AddScene<ExampleScene>();
                    services.AddScene<ExampleScene2>();

                    services.AddLogging();
                })
                .Build();

            host.Start();
        }
    }
}