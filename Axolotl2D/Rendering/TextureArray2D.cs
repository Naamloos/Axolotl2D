namespace Axolotl2D.Rendering;

/// <summary>A GPU texture array whose layers share identical dimensions.</summary>
public sealed class TextureArray2D
{
    private byte[]? pixels;
    private byte[]? normalPixels;
    internal uint Handle { get; set; }
    internal uint NormalHandle { get; set; }
    private readonly Texture2D[] fallbackTextures;
    private readonly Texture2D[]? fallbackNormalMaps;
    internal ReadOnlySpan<byte> Pixels => pixels;
    internal ReadOnlySpan<byte> NormalPixels => normalPixels;

    public int Width { get; }
    public int Height { get; }
    public int Layers { get; }

    public TextureArray2D(IReadOnlyList<Texture2D> layers, IReadOnlyList<Texture2D>? normalMaps = null)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (layers.Count == 0) throw new ArgumentException("At least one layer is required.", nameof(layers));
        if (normalMaps is not null && normalMaps.Count != layers.Count)
            throw new ArgumentException("Normal-map and color layer counts must match.", nameof(normalMaps));

        Width = layers[0].Width;
        Height = layers[0].Height;
        Layers = layers.Count;
        fallbackTextures = new Texture2D[layers.Count];
        if (normalMaps is not null) fallbackNormalMaps = new Texture2D[layers.Count];
        var layerBytes = checked(Width * Height * 4);
        pixels = new byte[checked(layerBytes * Layers)];
        normalPixels = new byte[pixels.Length];
        for (var layer = 0; layer < Layers; layer++)
        {
            var texture = layers[layer];
            fallbackTextures[layer] = texture;
            if (texture.Width != Width || texture.Height != Height)
                throw new ArgumentException("Every texture-array layer must have identical dimensions.", nameof(layers));
            var source = texture.PixelSpan;
            if (source.Length != layerBytes)
                throw new ArgumentException("Texture layers must retain readable RGBA pixels until array creation.", nameof(layers));
            source.CopyTo(pixels.AsSpan(layer * layerBytes, layerBytes));

            var destinationNormal = normalPixels.AsSpan(layer * layerBytes, layerBytes);
            if (normalMaps is not null)
            {
                var normal = normalMaps[layer];
                fallbackNormalMaps![layer] = normal;
                if (normal.Width != Width || normal.Height != Height || normal.PixelSpan.Length != layerBytes)
                    throw new ArgumentException("Every normal-map layer must match its color layer.", nameof(normalMaps));
                normal.PixelSpan.CopyTo(destinationNormal);
            }
            else
            {
                for (var offset = 0; offset < destinationNormal.Length; offset += 4)
                {
                    destinationNormal[offset] = 128;
                    destinationNormal[offset + 1] = 128;
                    destinationNormal[offset + 2] = 255;
                    destinationNormal[offset + 3] = 255;
                }
            }
        }
    }

    public Sprite GetSprite(int layer, TextureRegion? source = null)
    {
        if ((uint)layer >= (uint)Layers) throw new ArgumentOutOfRangeException(nameof(layer));
        return new Sprite(this, layer, source);
    }

    internal Texture2D GetFallbackTexture(int layer) => fallbackTextures[layer];
    internal Texture2D? GetFallbackNormalMap(int layer) => fallbackNormalMaps?[layer];

    internal void ReleasePixels()
    {
        pixels = null;
        normalPixels = null;
    }
}
