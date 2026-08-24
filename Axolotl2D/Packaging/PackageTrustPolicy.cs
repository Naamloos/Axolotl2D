using System.Security.Cryptography;

namespace Axolotl2D.Packages;

/// <summary>Separately controls package authenticity requirements and permission to execute module code.</summary>
public sealed class PackageTrustPolicy
{
    private PackageTrustPolicy(bool requireSignature, bool allowExecutableCode, IReadOnlyDictionary<string, string> trustedPublicKeys)
    {
        RequireSignature = requireSignature;
        AllowExecutableCode = allowExecutableCode;
        TrustedPublicKeys = new Dictionary<string, string>(trustedPublicKeys, StringComparer.Ordinal);
    }

    /// <summary>Whether an unsigned package must be rejected.</summary>
    public bool RequireSignature { get; }
    /// <summary>Whether a successfully validated package may load its managed code.</summary>
    public bool AllowExecutableCode { get; }
    /// <summary>Trusted signer IDs mapped to ECDSA public-key PEM text.</summary>
    public IReadOnlyDictionary<string, string> TrustedPublicKeys { get; }

    /// <summary>Requires a signature from the supplied keyring before optionally allowing code.</summary>
    public static PackageTrustPolicy RequireTrustedSignature(IReadOnlyDictionary<string, string> trustedPublicKeys,
        bool allowExecutableCode = true)
    {
        ArgumentNullException.ThrowIfNull(trustedPublicKeys);
        return new(true, allowExecutableCode, trustedPublicKeys);
    }

    /// <summary>Explicitly permits unsigned package code. This grants arbitrary code execution.</summary>
    public static PackageTrustPolicy AllowUnsignedExecutableCode() => new(false, true,
        new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Permits content but never loads the package assembly.</summary>
    public static PackageTrustPolicy ContentOnly(IReadOnlyDictionary<string, string>? trustedPublicKeys = null) =>
        new(false, false, trustedPublicKeys ?? new Dictionary<string, string>(StringComparer.Ordinal));

    internal void Validate(AxolotlPackage package)
    {
        if (package.Signature is null)
        {
            if (RequireSignature)
                throw new CryptographicException($"Package '{package.Manifest.Id}' must have a trusted signature.");
            return;
        }

        if (!TrustedPublicKeys.TryGetValue(package.Signature.SignerId, out var publicKey))
            throw new CryptographicException($"Package signer '{package.Signature.SignerId}' is not trusted.");
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKey);
        if (key.KeySize != 256)
            throw new CryptographicException($"Trusted key '{package.Signature.SignerId}' is not ECDSA P-256.");
        using var stream = File.OpenRead(package.Path);
        var hash = SHA256.HashData(new BoundedReadStream(stream, package.Signature.SignedLength, leaveOpen: true));
        if (!key.VerifyHash(hash, package.Signature.Value))
            throw new CryptographicException($"Package '{package.Manifest.Id}' has an invalid signature.");
    }
}
