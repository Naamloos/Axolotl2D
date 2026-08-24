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
                services.AddPrefabComponent<Spinner>("example.spinner");
                services.AddScene<SpriteScene>();
                services.AddScene<AnimationScene>();
                services.AddScene<PhysicsScene>();
                services.AddScene<LightingScene>();
                services.AddScene<CameraScene>();
                services.AddScene<RenderTargetScene>();
                services.AddScene<InputScene>();
                services.AddScene<ShaderScene>();
                services.AddScene<PostProcessScene>();
                services.AddScene<ParticleScene>();
                services.AddScene<PrefabScene>();
                services.AddScene<UIScene>();
                services.AddScene<SaveScene>();
            })
            .Build();

        await host.RunAsync();
    }
}
