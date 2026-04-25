# Changelog

All notable changes to **Michi** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-04-25

First public release. Michi ships the `MPath` type — a strongly-typed, immutable, normalized
absolute-path value object for .NET — to GitHub Packages. 0.y.z SemVer semantics apply: the
API may change between 0.x.y patches while real-consumer feedback comes in.

### Added

#### Core `MPath` type

- `MPath` sealed class with construction (`From`, `TryFrom`, `Format`), six-step
  normalization at construction time, and immutable derived properties (`Name`,
  `Extension`, `Segments`, `Depth`, `Root`).
- Host-OS-correct equality and hashing via a single `HostOs.PathComparer` source.
  `MPathComparer.Ordinal` and `MPathComparer.OrdinalIgnoreCase` are available for
  explicit semantics regardless of host OS.
- Navigation: `Parent`, `Up(int)`, `TryGetParent(out MPath?)`.
- Mutation (returns new instances): `WithName`, `WithExtension`, `WithoutExtension`.
- Joining: `/` operator and `Join(params string[])`. Leading separators on the
  right-hand side are stripped so the join always treats the RHS as relative.
- String output: `ToString()` (OS-native), `Value`, `Path` (compatibility alias),
  `ToUnixString()`, `ToWindowsString()`.
- Well-known paths: `MPath.Home`, `MPath.Temp`, `MPath.CurrentDirectory` (never cached),
  `MPath.InstalledDirectory`, `MPath.ApplicationData`, `MPath.LocalApplicationData`,
  `MPath.CommonApplicationData`.
- `MPathOptions` record with `BaseDirectory`, `ExpandTilde`, `ExpandEnvironmentVariables`.
  `MPathOptions.Default` is read-only; consumers use `with` expressions for per-call
  overrides.
- Exception hierarchy: `MPathException` base, `InvalidPathException`, `NoParentException`.

#### Filesystem extensions (opt-in `Michi.FileSystem` namespace)

- Typed existence: `FileExists()`, `DirectoryExists()`. There is no generic `Exists()`
  — kind-mismatch errors surface at the call site.
- Creation (idempotent): `CreateDirectory()`, `CreateOrClearDirectory()`,
  `EnsureParentExists()`.
- `System.IO` interop: `ToFileInfo()`, `ToDirectoryInfo()`.
- Lazy enumeration: `EnumerateFiles(pattern, SearchOption)`,
  `EnumerateDirectories(pattern, SearchOption)` — both return `IEnumerable<MPath>`.
- Idempotent, kind-typed deletion: `DeleteFile()`,
  `DeleteDirectory(recursive: true)`.
- Move and copy: `MoveTo`, `CopyTo` with the `[Flags] ExistsPolicy` enum. Named
  combinations: `Fail`, `MergeAndSkip`, `MergeAndOverwrite`, `MergeAndOverwriteIfNewer`.
  Auto-create missing parent on the target side. `CopyTo` accepts an optional recursive
  `Func<MPath, bool>` filter.

#### Release engineering

- Multi-target: `netstandard2.1`, `net8.0`, `net10.0`. Package id `Michi`.
- SourceLink, deterministic builds, `.snupkg` symbol packages, Package Validation.
- MinVer-derived version from git tags (`v*` prefix).
- GitHub Actions CI matrix: Ubuntu, Windows, and macOS on every PR.
- GitHub Actions release workflow: validate → pack → sign (Azure Key Vault via OIDC)
  → publish to GitHub Packages → CycloneDX SBOM attached to the GitHub Release.
- AOT-compatible: `<IsAotCompatible>true</IsAotCompatible>` with zero analyzer findings.

#### Documentation

- README cookbook covering construction, joining, normalization, equality, string
  output, navigation, mutation, well-known paths, tilde and env-var expansion, options,
  filesystem operations, the serialization workaround, and security.
- MIT LICENSE.
- `docs/superpowers/development.md` contributor guide (prerequisites, daily workflow,
  CI, releasing procedure, Azure Key Vault setup, dependency updates, troubleshooting).

### Security

- `MPath.ResolveContained(string)` and `MPath.TryResolveContained(string, out MPath?)`
  — ZIP-slip / CWE-22 defense. Lexical containment with a segment-boundary guard
  (no string-prefix confusion). Does NOT resolve symlinks; see the README "Security"
  section for adversarial-filesystem threat-model mitigations.

### Notes

- NuGet.org publication is deferred to the 1.0 milestone. 0.1.0 through 0.x.y release
  to GitHub Packages only. Consumers install via a GitHub NuGet source with a PAT that
  has `read:packages` scope.
- `MPathJsonConverter` and `MPathTypeConverter` are deferred to post-1.0 alongside
  `MPathRelative`. See the README "Serialization" section for the manual `string`
  round-trip workaround.
- `IsDescendantOf`, `IsAncestorOf`, and `GetRelativePathTo` are deferred (coupled with
  `MPathRelative`). `MPathScope` is deferred (YAGNI for 1.0).

[Unreleased]: https://github.com/nabeelio/Michi/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/nabeelio/Michi/releases/tag/v0.1.0
