using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Axolotl2D.Scenes;

/// <summary>Hosts the game and owns the active scene DI scope.</summary>
public sealed class SceneGameHost(
    Game game,
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime) : IGameHost
{
    private IServiceScope? currentScope;
    private Task? gameLoop;
    public BaseScene? CurrentScene { get; private set; }

    public void ChangeScene<T>() where T : BaseScene => ChangeScene(typeof(T));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        game.OnLoad += LoadDefaultScene;
        game.Closing += DisposeCurrentScene;
        try
        {
            await game.InitializeGameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            game.OnLoad -= LoadDefaultScene;
            game.Closing -= DisposeCurrentScene;
            throw;
        }

        gameLoop = Task.Run(game.Start, CancellationToken.None);
        _ = gameLoop.ContinueWith(
            _ => applicationLifetime.StopApplication(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        game.OnLoad -= LoadDefaultScene;
        game.Stop();
        if (gameLoop is not null)
            await gameLoop.ConfigureAwait(false);
        game.Closing -= DisposeCurrentScene;
        DisposeCurrentScene();
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
