# Using Packages in a Game

An `.axpkg` can contain assets, managed behavior, or both. The game chooses each package path and trust policy. Axolotl2D never scans a mod directory or loads a dependency from disk on its own.

Executable packages can add:

- scenes;
- GameObject factories and `Component` behavior;
- custom asset loaders;
- implementations of contracts defined by the game;
- code consumed by another package dependency.

The host remains in control. Loading a package makes its registrations available, but does not change the active scene or spawn objects.

## Base game assets

A signed base-content package keeps bulk content outside the game assembly:

```csharp
var official = PackageTrustPolicy.RequireTrustedSignature(publisherKeys);

await packages.LoadAsync("Content/Base.axpkg", official, cancellationToken);

var logo = await assets.LoadPackageAsync<Texture2D>(
    "ui/logo", "my.game.base", "sprites/logo", cancellationToken);
```

Load the package before a scene requests its assets. Asset names belong to a package, so two packages may both contain `sprites/logo`. The cache key, `ui/logo` above, belongs to the game.

Content-only packages work for textures, audio, fonts, maps, or data that use loaders available in the game. `PackageTrustPolicy.ContentOnly(...)` never loads the contained DLL, so it cannot register behavior or custom loaders.

## Signed DLC or an expansion

A DLC entrypoint registers its content after signature and dependency validation:

```csharp
public sealed class DlcModule : IAxolotlModule
{
    public void Initialize(AxolotlModuleContext context)
    {
        context.RegisterScene<DlcMapScene>("my.game.dlc1/map");
        context.RegisterGameObject("my.game.dlc1/boss", static (_, objects, name) =>
        {
            var boss = objects.Create<Boss>(name);
            boss.AddComponent<DlcBossBehavior>();
            return boss;
        });
    }
}
```

The game mounts official packages and enters registered DLC scenes by ID:

```csharp
await packages.LoadAsync("Content/Base.axpkg", official, cancellationToken);
await packages.LoadAsync("Content/Dlc1.axpkg", official, cancellationToken);

sceneHost.ChangeScene("my.game.dlc1/map");
```

The DLC manifest can require the exact base package version. The game must mount that version first. A missing or incompatible dependency stops the DLC before its code runs.

`SceneGameHost.ChangeScene(Type)` also accepts a scene type found at runtime. The ID overload avoids exposing package types to the game and supports menus built from `AxolotlModuleRegistry.Scenes`.

## Spawning package objects in an existing scene

An existing scene can place objects contributed by any mounted executable package:

```csharp
public override void Load()
{
    var boss = InstantiateRegistered("my.game.dlc1/boss", "Ash Warden");
    boss.Transform.Position = new Vector2(800, 320);
}
```

The factory runs with the active scene's scoped services and `IGameObjectFactory`. The returned object becomes owned by that scene and follows its normal start, update, render, destruction, and disposal lifecycle.

A package can reuse a base-game object and add behavior:

```csharp
context.RegisterGameObject("weather.mod/storm-cloud", static (services, objects, name) =>
{
    var cloud = objects.Create<BaseCloud>(name);
    cloud.AddComponent<LightningBehavior>();
    cloud.AddComponent(services.GetRequiredService<WeatherTypes>().WindComponentType);
    return cloud;
});
```

`GameObject.AddComponent(Type)`, `GetComponent(Type)`, and `RemoveComponent(Type)` support types discovered after the game was built. `BaseScene.Instantiate(Type, name)` does the same for a runtime `GameObject` subtype.

Package factories should create a new object on each call. They should not add it to another scene or retain the scene service provider after the object is destroyed.

## Game-specific extension contracts

Scenes and GameObjects cover engine lifecycle behavior. A game can expose a small contract for other systems such as quests, dialogue, procedural generation, rules, or UI panels:

```csharp
public interface IQuestProvider
{
    IEnumerable<Quest> CreateQuests(IServiceProvider services);
}
```

The contract should live in the game assembly or a shared contracts assembly loaded in the default context. The module references that assembly and registers an implementation:

```csharp
public void Initialize(AxolotlModuleContext context)
{
    context.RegisterExtension<IQuestProvider>(
        "lost-temple/quests",
        new LostTempleQuestProvider());
}
```

The game reads all current providers without scanning package assemblies:

```csharp
foreach (var provider in moduleRegistry.GetExtensions<IQuestProvider>().Values)
    questLog.AddRange(provider.CreateQuests(sceneServices));
```

The contract controls the capabilities available to extensions. `AxolotlModuleContext.Services` can resolve services already registered by the game, but a package cannot add registrations to the built DI container.

## Dynamic executable mods

The game owns mod discovery and user consent. A launcher may enumerate a user-selected directory, then pass each selected file to Axolotl2D:

```csharp
var modPolicy = PackageTrustPolicy.AllowUnsignedExecutableCode();

foreach (var path in enabledModPaths)
    await packages.LoadAsync(path, modPolicy, cancellationToken);

foreach (var scene in moduleRegistry.Scenes)
    modMenu.AddScene(scene.Id, scene.PackageId);
```

`enabledModPaths` comes from game or launcher configuration. Axolotl2D does not populate it.

An unsigned executable package has the same operating-system permissions as the game. Its code can read files, use the network, start processes, or corrupt game state. Use `AllowUnsignedExecutableCode()` only when the player has chosen to run code from those mod authors. Asset-only community packs can use `ContentOnly(...)` instead.

Load dependencies before dependent mods. Axolotl2D validates declared IDs and versions but does not locate missing files.

## Small patch packages

Version 1 packages do not overlay or replace entries in another mounted package. Give a patch its own package ID and make selection explicit:

```csharp
await packages.LoadAsync("Content/Base.axpkg", official, cancellationToken);

if (File.Exists("Content/Hotfix-1.axpkg"))
    await packages.LoadAsync("Content/Hotfix-1.axpkg", official, cancellationToken);

var sourcePackage = packages.MountedPackages.Any(
    package => package.Manifest.Id == "my.game.hotfix1")
        ? "my.game.hotfix1"
        : "my.game.base";

var balance = await assets.LoadPackageAsync<BalanceData>(
    "active/balance", sourcePackage, "balance/main", cancellationToken);
```

This pattern works for a corrected asset, a balance table, or a replacement script registered under a new ID. The game decides precedence. Two packages with the same package ID cannot be mounted together, and Axolotl2D does not hot-replace a mounted assembly.

## Localization and optional asset packs

Localization packs often need no code:

```csharp
await packages.LoadAsync(
    $"Content/Locale/{locale}.axpkg",
    PackageTrustPolicy.ContentOnly(publisherKeys),
    cancellationToken);

var strings = await assets.LoadPackageAsync<StringTable>(
    $"locale/{locale}", $"my.game.locale.{locale}", "strings", cancellationToken);
```

The runtime loader for `StringTable` must already exist in the game or a trusted executable dependency mounted before the content-only package is used.

## Reusing assets and code from other packages

Package assets use explicit package IDs:

```csharp
var atlas = await assets.LoadPackageAsync<Texture2D>(
    "dlc/shared-atlas", "my.game.base", "sprites/world-atlas", cancellationToken);
```

A module project can declare another module as a dependency:

```xml
<ProjectReference Include="..\BaseContent\BaseContent.csproj"
                  AxolotlModule="true"
                  AxolotlPackageId="my.game.base"
                  AxolotlPackageVersion="1.0.0" />
```

The dependent package may compile against public code from `BaseContent`. At runtime, its load context resolves that reference to the assembly from the mounted base package. Axolotl2D does not copy the dependency assembly into the dependent package.

Shared contracts used by the host and packages should come from one assembly identity. Keep the contract in the game/default context when the game calls it, or declare one mounted package as the dependency that owns the contract. Avoid copying the same contract DLL into several packages, because .NET would treat copies loaded in different contexts as different type identities.

## Package lifetime

Registrations remain valid while their package is mounted. Disposing `AxolotlPackageManager` removes registered scenes, factories, extensions, and asset loaders before unloading collectible module contexts. Version 1 has no individual hot-unload API. A game should mount its chosen package set during startup or another controlled loading phase.
