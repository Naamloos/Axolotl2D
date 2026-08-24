using System.Reflection;
using System.Runtime.Loader;

namespace Axolotl2D.MSBuild;

/// <summary>Build-time source-format importer. Importer assemblies never execute at game runtime.</summary>
public interface IContentImporter
{
    bool CanImport(ContentImporterContext context);
    ValueTask<ImportedAsset> ImportAsync(ContentImporterContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ContentImporterContext(string SourcePath, string LogicalName,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ImportedAsset(string LogicalName, string RuntimeType, string ContentType,
    byte[] Payload, IReadOnlyList<string>? Dependencies = null);

internal static class ContentImporters
{
    public static IReadOnlyList<IContentImporter> Load(IEnumerable<string> assemblyPaths)
    {
        var importers = new List<IContentImporter> { new PngImporter(), new TtfImporter(), new WavImporter() };
        foreach (var path in assemblyPaths)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
            foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IContentImporter).IsAssignableFrom(type)))
                importers.Add((IContentImporter)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"Could not create content importer '{type.FullName}'.")));
        }
        return importers;
    }
}

internal abstract class ExtensionImporter(string extension, string runtimeType, string contentType) : IContentImporter
{
    public bool CanImport(ContentImporterContext context) =>
        string.Equals(Path.GetExtension(context.SourcePath), extension, StringComparison.OrdinalIgnoreCase);

    public async ValueTask<ImportedAsset> ImportAsync(ContentImporterContext context,
        CancellationToken cancellationToken = default) => new(context.LogicalName, runtimeType, contentType,
        await File.ReadAllBytesAsync(context.SourcePath, cancellationToken).ConfigureAwait(false));
}

internal sealed class PngImporter() : ExtensionImporter(".png", "Axolotl2D.Rendering.Texture2D, Axolotl2D", "image/png");
internal sealed class TtfImporter() : ExtensionImporter(".ttf", "Axolotl2D.Assets.FontAsset, Axolotl2D", "font/ttf");
internal sealed class WavImporter() : ExtensionImporter(".wav", "Axolotl2D.Assets.SoundAsset, Axolotl2D", "audio/wav");
