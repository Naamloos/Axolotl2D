using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Axolotl2D.Packages;

namespace Axolotl2D.Scenes;

/// <summary>Controls whether a pushed scene keeps lower scenes active and visible.</summary>
public readonly record struct SceneLayerOptions(bool UpdateBelow, bool DrawBelow)
{
    public static SceneLayerOptions Overlay => new(UpdateBelow: false, DrawBelow: true);
    public static SceneLayerOptions Additive => new(UpdateBelow: true, DrawBelow: true);
    public static SceneLayerOptions Opaque => new(UpdateBelow: false, DrawBelow: false);
}

/// <summary>Hosts the game and owns the active scene DI scopes.</summary>
public sealed class SceneGameHost(
    Game game,
    IServiceScopeFactory scopeFactory,
    AxolotlModuleRegistry moduleRegistry,
    IHostApplicationLifetime applicationLifetime) : IGameHost
{
    private readonly List<SceneLayer> layers = [];
    private Task? gameLoop;
    private int layerVersion;

    /// <summary>The topmost active scene.</summary>
    public BaseScene? CurrentScene => layers.Count == 0 ? null : layers[^1].Scene;
    public int SceneCount => layers.Count;

    public void ChangeScene<T>() where T : BaseScene => ChangeScene(typeof(T));

    /// <summary>Changes to a package scene registered under a stable ID.</summary>
    public void ChangeScene(string id) => ChangeScene(moduleRegistry.GetScene(id).SceneType);

    /// <summary>Pushes an overlay scene that blocks lower updates while drawing over them.</summary>
    public void PushScene<T>(SceneLayerOptions? options = null) where T : BaseScene =>
        PushScene(typeof(T), options);

    /// <summary>Pushes a package scene registered under a stable ID.</summary>
    public void PushScene(string id, SceneLayerOptions? options = null) =>
        PushScene(moduleRegistry.GetScene(id).SceneType, options);

    /// <summary>Removes and disposes the top scene, preserving the base scene.</summary>
    public bool PopScene()
    {
        if (layers.Count <= 1)
            return false;

        var layer = layers[^1];
        layers.RemoveAt(layers.Count - 1);
        layerVersion++;
        DisposeLayer(layer);
        return true;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        game.OnLoad += LoadDefaultScene;
        game.OnUpdate += UpdateScenes;
        game.OnDraw += RenderScenes;
        game.OnResize += ResizeScenes;
        game.Closing += DisposeScenes;
        try
        {
            await game.InitializeGameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DetachEvents();
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
        try
        {
            if (gameLoop is not null)
                await gameLoop.ConfigureAwait(false);
        }
        finally
        {
            DetachEvents();
            DisposeScenes();
        }
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

    /// <summary>Changes to a scene type discovered at runtime and clears every active layer.</summary>
    public void ChangeScene(Type type)
    {
        var next = CreateLayer(type, SceneLayerOptions.Opaque);
        try
        {
            DisposeScenes();
        }
        catch
        {
            next.Scope.Dispose();
            throw;
        }
        ActivateLayer(next);
    }

    /// <summary>Pushes a scene type discovered at runtime.</summary>
    public void PushScene(Type type, SceneLayerOptions? options = null) =>
        ActivateLayer(CreateLayer(type, options ?? SceneLayerOptions.Overlay));

    private SceneLayer CreateLayer(Type type, SceneLayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsAssignableTo(typeof(BaseScene)) || type.IsAbstract)
            throw new ArgumentException($"{type.FullName} must be a concrete BaseScene type.", nameof(type));

        var scope = scopeFactory.CreateScope();
        try
        {
            var scene = (scope.ServiceProvider.GetService(type)
                ?? ActivatorUtilities.CreateInstance(scope.ServiceProvider, type)) as BaseScene
                ?? throw new InvalidOperationException($"{type.Name} could not be created as a scene.");
            scene.sceneGameHost = this;
            scene.game = game;
            scene.Initialize(scope.ServiceProvider);
            return new(scene, scope, options);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private void ActivateLayer(SceneLayer layer)
    {
        layers.Add(layer);
        layerVersion++;
        try
        {
            layer.Scene.LoadScene();
        }
        catch
        {
            layers.Remove(layer);
            layerVersion++;
            DisposeLayer(layer);
            throw;
        }
    }

    private void UpdateScenes(double deltaTime)
    {
        if (layers.Count == 0)
            return;

        var first = layers.Count - 1;
        while (first > 0 && layers[first].Options.UpdateBelow)
            first--;

        var version = layerVersion;
        for (var index = first; index < layers.Count; index++)
        {
            layers[index].Scene.Tick(deltaTime);
            if (version != layerVersion)
                break;
        }
    }

    private void RenderScenes(double deltaTime, double frameRate)
    {
        if (layers.Count == 0)
            return;

        var first = layers.Count - 1;
        while (first > 0 && layers[first].Options.DrawBelow)
            first--;

        var version = layerVersion;
        for (var index = first; index < layers.Count; index++)
        {
            layers[index].Scene.Render(deltaTime, frameRate);
            if (version != layerVersion)
                break;
        }
    }

    private void ResizeScenes(System.Numerics.Vector2 size)
    {
        var version = layerVersion;
        for (var index = 0; index < layers.Count; index++)
        {
            layers[index].Scene.Resize(size);
            if (version != layerVersion)
                break;
        }
    }

    private void DisposeScenes()
    {
        if (layers.Count == 0)
            return;

        var active = layers.ToArray();
        layers.Clear();
        layerVersion++;
        Exception? failure = null;
        for (var index = active.Length - 1; index >= 0; index--)
            try
            {
                DisposeLayer(active[index]);
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        if (failure is not null)
            throw failure;
    }

    private static void DisposeLayer(SceneLayer layer)
    {
        try
        {
            layer.Scene.UnloadScene();
        }
        finally
        {
            layer.Scope.Dispose();
        }
    }

    private void DetachEvents()
    {
        game.OnLoad -= LoadDefaultScene;
        game.OnUpdate -= UpdateScenes;
        game.OnDraw -= RenderScenes;
        game.OnResize -= ResizeScenes;
        game.Closing -= DisposeScenes;
    }

    private sealed record SceneLayer(BaseScene Scene, IServiceScope Scope, SceneLayerOptions Options);
}
