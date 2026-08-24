# `.axpkg` File Format

This document specifies Axolotl package format version 1. Axolotl2D identifies a package from its magic bytes and validates its structure before it reads metadata or loads code.

## Encoding rules

- Multi-byte integers use little-endian byte order.
- Strings use UTF-8 without a byte-order mark. Length fields count bytes, not characters.
- `uint16`, `int32`, and `int64` have widths of 2, 4, and 8 bytes.
- Offsets in this document start at zero.

## Package layout

```text
package header
entry 0
entry 1
...
entry N - 1
signature trailer, when flags bit 0 is set
```

The format has no central offset table. A reader scans entry headers, records each payload offset and length, and seeks to payloads on demand. The reader does not need to load the package into memory.

## Package header

The header occupies 16 bytes.

| Offset | Size | Type | Field | Version 1 value |
|---:|---:|---|---|---|
| 0 | 8 | bytes | Magic | `41 58 50 4B 47 0D 0A 1A` (`AXPKG\r\n\x1A`) |
| 8 | 2 | `uint16` | Format version | `1` |
| 10 | 2 | `uint16` | Flags | See below |
| 12 | 4 | `int32` | Entry count | `1` through `100000` |

Version 1 defines one flag:

| Bit | Mask | Meaning |
|---:|---:|---|
| 0 | `0x0001` | A signature trailer follows the final entry |

Readers reject any other flag bit in version 1.

## Entry layout

Each entry starts with a 16-byte header, followed by two strings and the payload.

| Relative offset | Size | Type | Field |
|---:|---:|---|---|
| 0 | 4 | `int32` | UTF-8 name length |
| 4 | 4 | `int32` | UTF-8 content-type length |
| 8 | 8 | `int64` | Payload length |
| 16 | name length | bytes | UTF-8 entry name |
| 16 + name length | content-type length | bytes | UTF-8 content type |
| 16 + name length + content-type length | payload length | bytes | Payload |

An entry name uses `/` as its separator. It cannot start with `/`, contain `\`, or contain empty, `.` or `..` path segments. Entry names are case-sensitive. A package cannot contain duplicate entry names.

The content type describes the payload representation. Readers can index and skip an entry whose content type they do not understand because the entry header supplies its payload length.

## Reserved entries

Every package contains one manifest entry:

| Property | Value |
|---|---|
| Name | `$module/manifest.json` |
| Content type | `application/vnd.axolotl2d.module+json` |
| Payload | UTF-8 JSON manifest |

The package contains its primary module assembly at the entry named by the manifest's `assembly` property. Managed assembly entries use `application/vnd.microsoft.portable-executable`.

Build tools place imported asset payloads under `$assets/` by convention. The manifest maps public asset names to those entries, so runtimes do not infer asset identity from the entry path.

Built-in `.axprefab` assets use content type `application/vnd.axolotl2d.prefab+json` and runtime type `Axolotl2D.Prefabs.PrefabAsset, Axolotl2D`. They remain ordinary manifest assets and do not alter the package binary format.

## Manifest

The version 1 writer emits compact UTF-8 JSON with camel-case property names. It emits properties in the order shown below and preserves dependency and asset declaration order.

```json
{
  "id": "my.game.content",
  "version": "1.0.0",
  "formatVersion": 1,
  "assembly": "$module/My.Game.Content.dll",
  "registrationType": "Axolotl2D.Generated.AxolotlGeneratedModuleRegistration",
  "entrypoint": null,
  "signerId": "publisher",
  "dependencies": [
    {
      "id": "my.game.base",
      "version": "1.0.0"
    }
  ],
  "assets": [
    {
      "name": "logo",
      "entry": "$assets/logo",
      "runtimeType": "Axolotl2D.Rendering.Texture2D, Axolotl2D",
      "dependencies": []
    }
  ]
}
```

| Property | Required | Meaning |
|---|---|---|
| `id` | Yes | Stable module ID |
| `version` | Yes | Module version |
| `formatVersion` | Yes | Must match the package header version |
| `assembly` | Yes | Entry name of the primary module DLL |
| `registrationType` | No | Exact generated registration type name |
| `entrypoint` | No | Exact `IAxolotlModule` implementation name |
| `signerId` | Signed packages | Logical signer ID, also stored in the signature trailer |
| `dependencies` | Yes | Required package IDs and exact versions |
| `assets` | Yes | Logical asset names, payload entries, runtime types, and optional asset dependencies |

Module and signer IDs contain at most 256 ASCII letters, digits, `.`, `-`, or `_`. Versions use a three-part numeric core such as `1.2.3` and may include prerelease or build suffixes. Entry and asset names follow the path rules above.

The manifest does not establish trust. In particular, `signerId` selects a public key from the game's keyring. A key stored inside package content would remain untrusted.

## Signature trailer

When header flags contain `0x0001`, the signature trailer starts after the final entry payload.

| Relative offset | Size | Type | Field | Version 1 value |
|---:|---:|---|---|---|
| 0 | 8 | bytes | Signature magic | `41 58 53 49 47 0D 0A 1A` (`AXSIG\r\n\x1A`) |
| 8 | 2 | `uint16` | Algorithm | `1` |
| 10 | 4 | `int32` | UTF-8 signer ID length | Non-negative |
| 14 | 4 | `int32` | Signature length | `64` |
| 18 | signer ID length | bytes | UTF-8 signer ID | Must match manifest `signerId` |
| 18 + signer ID length | 64 | bytes | Signature | IEEE P1363 `r || s` form |

Algorithm `1` means ECDSA P-256 with SHA-256. The signer hashes all bytes from package offset 0 up to, but excluding, the signature magic. The signer then signs that SHA-256 digest. The covered region includes the header, manifest, dependency declarations, assets, and module assemblies.

The trailer does not contain a public key. The loading game supplies trusted public keys through its trust policy. A reader rejects an invalid signature without treating the package as unsigned.

## Validation limits

The version 1 reader applies these limits before it loads executable code:

| Value | Limit |
|---|---:|
| Entries | `100000` |
| Entry name or content-type UTF-8 bytes | `1048576` |
| Entry payload | 8 GiB |
| Manifest payload | 4 MiB |
| Module assembly loaded for execution | 512 MiB |
| Entry or asset name characters | `1024` |
| Signature bytes | Exactly `64` |

The reader also rejects negative lengths, truncated data, invalid UTF-8, unsupported versions or flags, malformed metadata, duplicate names, malformed signature data, and trailing bytes after the expected package end.

## Extension rules

Version 1 readers skip entries with unknown content types. Manifest JSON may gain properties because the runtime ignores properties it does not use. New header flags, signature algorithms, or incompatible binary layout changes require a later package format version.
