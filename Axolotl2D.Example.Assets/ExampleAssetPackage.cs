using Axolotl2D.Packages;

namespace Axolotl2D.Example.Assets;

public static class ExampleAssetPackage
{
    public const string Id = "axolotl2d.example.assets";
    public const string FileName = "Axolotl2D.Example.Assets.axpkg";
    public const string SignerId = "axolotl2d-example-test";

    // EXAMPLE / TEST KEY ONLY. DO NOT USE THE MATCHING PRIVATE KEY IN A REAL GAME.
    public const string PublicKey = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAElRJWK4AV7TzQMt6YgJ+N9fNcCD3L
        KstIarIuUU0T51kMl1Tvuo325oXZhb6ro/jyjibMnHXaWW7Rlzbb5FhInA==
        -----END PUBLIC KEY-----
        """;

    public static PackageTrustPolicy TrustPolicy() => PackageTrustPolicy.RequireTrustedSignature(
        new Dictionary<string, string>(StringComparer.Ordinal) { [SignerId] = PublicKey });
}
