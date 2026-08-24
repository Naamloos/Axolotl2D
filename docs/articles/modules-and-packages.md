# Modules and `.axpkg` Packages

An Axolotl module is a reusable .NET class library. An `.axpkg` is its versioned container: deterministic metadata, compiled assets, and the module DLL. The package reader checks binary magic and structure instead of trusting the extension.

See the [`.axpkg` file format](axpkg-format.md) for the byte layout, manifest schema, signature trailer, and validation limits.
See [Using packages in a game](package-use-cases.md) for base content, DLC, mods, patch packages, dynamic scenes and GameObjects, and cross-package reuse.

Axolotl2D does not search for packages. A game mounts each package by path and chooses its trust policy.

## Create a module

Reference `Axolotl2D` and the `Axolotl2D.MSBuild` project/package, enable the module targets, and declare assets:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <AxolotlModule>true</AxolotlModule>
  <AxolotlPackageId>my.game.content</AxolotlPackageId>
  <AxolotlPackageVersion>1.0.0</AxolotlPackageVersion>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\Axolotl2D\Axolotl2D.csproj" />
  <ProjectReference Include="..\Axolotl2D.MSBuild\Axolotl2D.MSBuild.csproj"
                    ReferenceOutputAssembly="false" Private="false" />
  <AxolotlAsset Include="Assets\**\*" />
  <AxolotlAsset Update="Assets\Sprites\logo.png" Name="logo" />
</ItemGroup>

<Import Project="..\Axolotl2D.MSBuild\build\Axolotl2D.MSBuild.targets" />
```

`dotnet build` imports changed source assets into `obj`, generates module registration C#, compiles the normal DLL, then creates `bin/.../MyModule.axpkg`. PNG, TTF, WAV, and `.axprefab` importers preserve their source representation for the built-in runtime loaders. The prefab importer also validates its versioned JSON structure. Names use `/`, never platform-specific separators; duplicates fail the build.

## Custom importers and loaders

A build-time importer implements `IContentImporter` from `Axolotl2D.MSBuild`. Put its assembly in `AxolotlImporterAssembly`. Set an asset's `Importer` metadata when extension selection is insufficient. Importers may emit any compiled representation and runtime type.

Runtime decoding remains separate. Implement `IAssetLoader<T>` in the module and declare it for generated registration:

```xml
<AxolotlAssetLoader Include="DialogueLoader"
                     AssetType="MyGame.Dialogue"
                     LoaderType="MyModule.DialogueLoader" />
```

Importer code runs only during the build. Runtime loader and entrypoint code runs only after the package passes the selected trust policy.

## Signing and mounting

Set `AxolotlSigningKey` to an ECDSA P-256 private-key PEM and `AxolotlSignerId` to its logical ID. The private key is a build input and is never stored in the package. The signature covers the complete package before its signature trailer, including metadata, dependencies, assets, and DLLs.

The game supplies trusted public keys and mounts the package:

```csharp
var policy = PackageTrustPolicy.RequireTrustedSignature(
    new Dictionary<string, string> { ["publisher"] = publicKeyPem });

await packages.LoadAsync("Content.axpkg", policy, cancellationToken);
await assets.LoadPackageAsync<Texture2D>(
    "logo", "my.game.content", "logo", cancellationToken);
```

Available policies separate authenticity from code execution:

- `RequireTrustedSignature(...)` requires a known valid signature before code can execute.
- `ContentOnly(...)` accepts unsigned content but never loads its DLL. A present signature must still be known and valid.
- `AllowUnsignedExecutableCode()` explicitly permits unsigned managed code.

Allowing unsigned executable modules is equivalent to allowing arbitrary code execution. Enable it only for games intentionally supporting trusted or user-installed code mods. A signed package with an invalid or unknown signature is always rejected, never treated as unsigned.

## Dependencies and deployment

Use a project reference marked as a module dependency:

```xml
<ProjectReference Include="..\BaseContent\BaseContent.csproj"
                  AxolotlModule="true"
                  AxolotlPackageId="my.game.base"
                  AxolotlPackageVersion="1.0.0" />
```

MSBuild builds the dependency first and records its exact version without embedding it in the dependent package. Games mount dependencies before dependents; Axolotl2D does not search disk for them. On a game project reference, the same metadata tells MSBuild to copy the resulting `.axpkg` to the game output without adding the module DLL as a compile reference.

An optional module entrypoint implements `IAxolotlModule`. Select it with `AxolotlModuleEntrypoint`. An executable entrypoint can register asset loaders, scenes, GameObject factories, or implementations of contracts defined by the game:

```csharp
public sealed class Module : IAxolotlModule
{
    public void Initialize(AxolotlModuleContext context)
    {
        context.RegisterScene<ChallengeScene>("my.dlc/challenge");
        context.RegisterGameObject("my.dlc/enemy", static (_, objects, name) =>
        {
            var enemy = objects.Create(name);
            enemy.AddComponent<ChallengeEnemy>();
            return enemy;
        });
    }
}
```

The game can change to the scene with `SceneGameHost.ChangeScene("my.dlc/challenge")`. An active scene can call `InstantiateRegistered("my.dlc/enemy")`. Trusted modules may also call `RegisterPrefabComponent<T>(id)` for components implementing `IPrefabDataReceiver`. Content-only packages can contain `.axprefab` assets but cannot register executable component types. IDs share one game-wide namespace, so prefix them with the package ID.

See [Data Prefabs](prefabs.md) for packaging and instantiating pure-data GameObject hierarchies.

The module context exposes the existing root `IServiceProvider` for resolving game services. It does not allow modules to mutate the built DI container. Use `RegisterExtension<TContract>` when a package needs to contribute behavior to a game-owned system. Declare extra module-local DLLs with `AxolotlModuleAssembly`.
