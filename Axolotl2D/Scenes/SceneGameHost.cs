using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Axolotl2D.Scenes;

/// <summary>Hosts the game and owns the active scene DI scope.</summary>
public sealed class SceneGameHost(Game game, IServiceScopeFactory scopeFactory) : IGameHost
{
    private IServiceScope? currentScope;
    public BaseScene? CurrentScene { get; private set; }

    public void ChangeScene<T>() where T : BaseScene => ChangeScene(typeof(T));

    public Task StartAsync(CancellationToken cancellationToken)
    {
        game.OnLoad += LoadDefaultScene;
        return Task.Run(game.Start, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeCurrentScene();
        return Task.Run(game.Stop, cancellationToken);
    }

    private void LoadDefaultScene()
    {
        var scenes = Assembly.GetEntryAssembly()!.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(BaseScene)) && type.GetCustomAttribute<DefaultSceneAttribute>() is not null)
            .ToArray();

        if (scenes.Length != 1)
            throw new InvalidOperationException(scenes.Length == 0
                ? "No scene has DefaultSceneAttribute."
                : "Only one scene can have DefaultSceneAttribute.");

        ChangeScene(scenes[0]);
        game.OnLoad -= LoadDefaultScene;
    }

    private void ChangeScene(Type type)
    {
        var nextScope = scopeFactory.CreateScope();
        BaseScene nextScene;
        try
        {
            nextScene = nextScope.ServiceProvider.GetRequiredService(type) as BaseScene
                ?? throw new InvalidOperationException($"{type.Name} is not a registered scene.");
        }
        catch
        {
            nextScope.Dispose();
            throw;
        }

        try
        {
            DisposeCurrentScene();
        }
        catch
        {
            nextScope.Dispose();
            throw;
        }
        currentScope = nextScope;
        CurrentScene = nextScene;
        CurrentScene.sceneGameHost = this;
        CurrentScene.game = game;
        CurrentScene.Initialize(nextScope.ServiceProvider);

        try
        {
            CurrentScene.LoadScene();
            game.OnUpdate += CurrentScene.Tick;
            game.OnDraw += CurrentScene.Render;
            game.OnResize += CurrentScene.Resize;
        }
        catch
        {
            DisposeCurrentScene();
            throw;
        }
    }

    private void DisposeCurrentScene()
    {
        try
        {
            if (CurrentScene is not null)
            {
                game.OnUpdate -= CurrentScene.Tick;
                game.OnDraw -= CurrentScene.Render;
                game.OnResize -= CurrentScene.Resize;
                CurrentScene.UnloadScene();
            }
        }
        finally
        {
            CurrentScene = null;
            currentScope?.Dispose();
            currentScope = null;
        }
    }
}
