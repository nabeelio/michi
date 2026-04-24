using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Michi.Converters;
using Michi.Exceptions;
using Michi.Internal;
// Alias to disambiguate from the new instance-level Path property on MPath.
using SysPath = System.IO.Path;

namespace Michi;

/// <summary>
/// A normalized, immutable absolute filesystem path. Equality is host-OS aware.
/// </summary>
/// <remarks>
/// Construct via <see cref="From(string, MPathOptions?)" /> or <see cref="TryFrom" />.
/// The internal canonical form is forward-slash; <see cref="ToString" /> and the
/// <see cref="Value" /> property both return the OS-native-separator form (backslash
/// on Windows, forward-slash elsewhere) so paths drop into `System.IO` and most
/// third-party string-typed APIs without extra conversion. <see cref="Path" /> remains
/// as a compatibility alias for <see cref="Value" />. Use <see cref="ToUnixString" />
/// when you need a deterministic, platform-independent string (logging, JSON, snapshots).
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[JsonConverter(typeof(MPathJsonConverter))]
[TypeConverter(typeof(MPathTypeConverter))]
public sealed class MPath : IEquatable<MPath>, IComparable<MPath> {
    // Directory separator characters, cached so IndexOfAny calls don't allocate a new
    // char[] on every invocation.
    private static readonly char[] DirectorySeparators = [
        '/',
        '\\',
    ];

    // Canonical forward-slash form. Derived values compute on demand via System.IO.Path.
    // The OS-native form is stored as the Value auto-property below and assigned once
    // in the ctor so ToString()/Value/Path are O(1) allocation-free.
    private readonly string _path;

    // Lazy segment cache. Default ImmutableArray<string> is `IsDefault == true`; we use
    // that as the "not yet computed" sentinel. First reader does the split, subsequent
    // readers return the cached array. Races are benign (worst case: split runs twice).
    private ImmutableArray<string> _segments;

    /// <summary>
    /// Trusted ctor for paths already produced by the normalizer, or derived from another
    /// MPath via substring operations that preserve invariants.
    /// </summary>
    private MPath(string normalizedPath, string root)
    {
        _path = normalizedPath;
        Value = HostOs.IsWindows ? normalizedPath.Replace('/', '\\') : normalizedPath;
        Root = root;
    }

    /// <summary>
    /// Root token in forward-slash form. "/" on Unix; "C:/" on Windows drive paths;
    /// "//server/share" on UNC.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// The final segment (file or directory name). Empty string if this path is the root.
    /// </summary>
    public string Name => _path == Root ? string.Empty : SysPath.GetFileName(_path);

    /// <summary>
    /// Final segment without the extension. "foo.txt" gives "foo"; ".bashrc" gives ".bashrc"
    /// (hidden files don't count as having an extension).
    /// </summary>
    public string NameWithoutExtension {
        get {
            var name = Name;
            if (name.Length == 0) {
                return string.Empty;
            }

            // dot > 0 excludes the leading-dot case (hidden files).
            var dot = name.LastIndexOf('.');

            return dot > 0 ? name[..dot] : name;
        }
    }

    /// <summary>
    /// Extension with the leading dot (".txt"), or empty string if none. The leading dot on
    /// a hidden file (".bashrc") doesn't count.
    /// </summary>
    public string Extension {
        get {
            var name = Name;
            if (name.Length == 0) {
                return string.Empty;
            }

            var dot = name.LastIndexOf('.');

            return dot > 0 ? name[dot..] : string.Empty;
        }
    }

    /// <summary>
    /// Whether this path has a file extension. Checks directly without materializing
    /// the <see cref="Extension" /> substring.
    /// </summary>
    public bool HasExtension {
        get {
            var name = Name;
            if (name.Length == 0) {
                return false;
            }

            var dot = name.LastIndexOf('.');

            // dot > 0 matches the hidden-file rule: a leading dot doesn't count.
            return dot > 0;
        }
    }

    /// <summary>
    /// Number of segments below the root. "/" is 0, "/a" is 1, "/a/b/c" is 3.
    /// </summary>
    public int Depth {
        get {
            if (_path == Root) {
                return 0;
            }

            #if NET8_0_OR_GREATER
            return _path.AsSpan(Root.Length).Count('/') + 1;
            #else
            var count = 0;
            for (var i = Root.Length; i < _path.Length; i++) {
                if (_path[i] == '/') {
                    count++;
                }
            }

            return count + 1;
            #endif
        }
    }

    /// <summary>
    /// Segments below the root, in order. Computed once on first access and cached. Read-only;
    /// caller can't mutate internal state.
    /// </summary>
    public IReadOnlyList<string> Segments {
        get {
            var cached = _segments;
            if (!cached.IsDefault) {
                return cached;
            }

            if (_path == Root) {
                _segments = [];

                return _segments;
            }

            var below = _path.AsSpan(Root.Length);
            if (below.Length > 0 && below[0] == '/') {
                below = below[1..];
            }

            _segments = [
                ..below.ToString().Split('/'),
            ];

            return _segments;
        }
    }

#region Construction

    /// <summary>
    /// Normalizes `path` and returns an <see cref="MPath" />.
    /// Relative inputs are resolved against <see cref="MPathOptions.BaseDirectory" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// `path` is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// Path cannot be normalized to a valid absolute path.
    /// </exception>
    public static MPath From(string path, MPathOptions? options = null)
    {
        Guard.NotNull(path, "Path cannot be null. Use TryFrom to accept null input without exceptions.");

        var result = PathNormalizer.Normalize(path, options ?? MPathOptions.Default);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Normalizes `path` using `relativeTo` as the base
    /// for relative-path resolution (overriding <see cref="MPathOptions.BaseDirectory" />).
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// `path` or `relativeTo` is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// Path cannot be normalized to a valid absolute path.
    /// </exception>
    public static MPath From(string path, string relativeTo, MPathOptions? options = null)
    {
        Guard.NotNull(path, "Path cannot be null. Use TryFrom to accept null input without exceptions.");
        Guard.NotNull(
            relativeTo,
            "relativeTo cannot be null. Pass an absolute path string to resolve relative inputs against."
        );

        var result = PathNormalizer.Normalize(path, options ?? MPathOptions.Default, relativeTo);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Non-throwing construction. Returns `true` and sets `result`
    /// if `path` is normalizable; returns `false` with null result otherwise.
    /// Accepts null input without throwing.
    /// </summary>
    public static bool TryFrom(string? path, out MPath? result, MPathOptions? options = null)
    {
        if (path is null) {
            result = null;

            return false;
        }

        try {
            result = From(path, options);

            return true;
        } catch (InvalidPathException) {
            result = null;

            return false;
        }
    }

    /// <summary>
    /// Substitutes `args` into `template` via
    /// <see cref="string.Format(IFormatProvider, string, object?[])" /> and constructs an
    /// <see cref="MPath" />. Uses <see cref="CultureInfo.InvariantCulture" /> so numeric and
    /// date formatting is locale-stable.
    /// </summary>
    /// <remarks>
    /// <strong>Security:</strong> unvetted user input in `args` can inject
    /// `..` segments that escape the intended base. For untrusted input, compose via the
    /// `/` operator (which strips leading separators) and validate the result.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// `template` is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// The substituted result is not a valid path.
    /// </exception>
    /// <exception cref="FormatException">
    /// `template` is malformed.
    /// </exception>
    public static MPath Format(string template, params object?[] args)
    {
        Guard.NotNull(
            template,
            "Format template cannot be null. Provide a string.Format-style template."
        );

        var substituted = string.Format(CultureInfo.InvariantCulture, template, args);

        var badIndex = substituted.IndexOfAny(SysPath.GetInvalidPathChars());
        if (badIndex >= 0) {
            var ch = substituted[badIndex];
            var display = char.IsControl(ch) ? ' ' : ch;

            throw new InvalidPathException(
                substituted,
                $"Contains invalid path character '{display}' (0x{(int) ch:X4}) at index {badIndex}. See Path.GetInvalidPathChars() for the current platform ({(HostOs.IsWindows ? "Windows" : "Unix")})."
            );
        }

        return From(substituted);
    }

#endregion

#region String output

    /// <summary>
    /// OS-native separator form: `/foo/bar` on Unix, `\foo\bar` on Windows.
    /// Precomputed at construction, so this is O(1) and allocation-free.
    /// </summary>
    /// <remarks>
    /// For deterministic output across platforms (logging, JSON, snapshots, hashes), use
    /// <see cref="ToUnixString" /> instead. The <see cref="Value" /> property returns the
    /// same value as this method; <see cref="Path" /> is a compatibility alias.
    /// </remarks>
    public override string ToString() => Value;

    /// <summary>
    /// The primary string form of this path, in OS-native separators. Equivalent to
    /// <see cref="ToString" /> and intended for LINQ projections, data binding, and
    /// pass-through to string-typed APIs.
    /// </summary>
    /// <example>
    ///     <code>
    /// File.ReadAllText(myPath.Value);
    /// var names = paths.Select(p => p.Value);
    /// </code>
    /// </example>
    public string Value { get; }

    /// <summary>
    /// Compatibility alias for <see cref="Value" />. Equivalent to <see cref="ToString" />.
    /// </summary>
    public string Path => Value;

    /// <summary>
    /// Forward-slash form, identical on every platform. Use this when you need a stable,
    /// deterministic string (logs you diff across OSes, JSON payloads, cache keys).
    /// </summary>
    public string ToUnixString() => _path;

    /// <summary>
    /// Backslash form. Useful when targeting Windows-specific APIs from a non-Windows build.
    /// </summary>
    public string ToWindowsString() => _path.Replace('/', '\\');

#endregion

#region Well-known paths

    /// <summary>
    /// Current user's home directory. Computed once at first access.
    /// </summary>
    public static MPath Home => WellKnownPaths.Home.Value;

    /// <summary>
    /// System temporary directory. Computed once at first access.
    /// </summary>
    public static MPath Temp => WellKnownPaths.Temp.Value;

    /// <summary>
    /// Directory containing the running application's main assembly. Stable across
    /// framework-dependent, self-contained, single-file, and test-host deployments.
    /// </summary>
    /// <remarks>
    /// Resolves via <see cref="AppContext.BaseDirectory" />. Not the same as
    /// <see cref="CurrentDirectory" /> -- the CWD is mutable and launch-context dependent;
    /// the installed directory is fixed for the process lifetime.
    /// </remarks>
    public static MPath InstalledDirectory => WellKnownPaths.InstalledDirectory.Value;

    /// <summary>
    /// Per-user application data intended to follow the user across machines. On Windows
    /// with a roaming profile, contents sync at logon/logoff; macOS and Linux have no
    /// roaming concept, so this is effectively user-local on those platforms.
    /// </summary>
    /// <remarks>
    /// Resolves via <see cref="Environment.SpecialFolder.ApplicationData" />:
    /// Windows → `%APPDATA%` (e.g. `C:\Users\{user}\AppData\Roaming`);
    /// macOS → `~/Library/Application Support` (same path as <see cref="LocalApplicationData" />);
    /// Linux → `$XDG_CONFIG_HOME`, else `~/.config`.
    /// Callers should create a subdirectory named after their application rather than write
    /// files directly here. Computed once at first access.
    /// </remarks>
    public static MPath ApplicationData => WellKnownPaths.ApplicationData.Value;

    /// <summary>
    /// Per-user, machine-local application data. Appropriate for caches, machine-specific
    /// configuration, and data that should not roam across machines.
    /// </summary>
    /// <remarks>
    /// Resolves via <see cref="Environment.SpecialFolder.LocalApplicationData" />:
    /// Windows → `%LOCALAPPDATA%` (e.g. `C:\Users\{user}\AppData\Local`);
    /// macOS → `~/Library/Application Support` (same path as <see cref="ApplicationData" />);
    /// Linux → `$XDG_DATA_HOME`, else `~/.local/share`.
    /// Callers should create a subdirectory named after their application rather than write
    /// files directly here. Computed once at first access.
    /// </remarks>
    public static MPath LocalApplicationData => WellKnownPaths.LocalApplicationData.Value;

    /// <summary>
    /// Machine-wide application data shared by all users of the system. Writes typically
    /// require elevated privileges.
    /// </summary>
    /// <remarks>
    /// Resolves via <see cref="Environment.SpecialFolder.CommonApplicationData" />:
    /// Windows → `%ProgramData%` (e.g. `C:\ProgramData`);
    /// macOS and Linux → `/usr/share`.
    /// Callers should create a subdirectory named after their application rather than write
    /// files directly here. Computed once at first access.
    /// </remarks>
    public static MPath CommonApplicationData => WellKnownPaths.CommonApplicationData.Value;

    /// <summary>
    /// Process current working directory. Evaluated on every access -- the process CWD is mutable.
    /// </summary>
    public static MPath CurrentDirectory => From(Directory.GetCurrentDirectory());

#endregion

#region Equality, comparison, operators

    /// <summary>
    /// Value equality by normalized path using <see cref="HostOs.PathComparer" />.
    /// Case-insensitive on Windows and macOS, case-sensitive on Linux.
    /// </summary>
    public bool Equals(MPath? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        return other is not null && HostOs.PathComparer.Equals(_path, other._path);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MPath other && Equals(other);

    /// <summary>
    /// Hash code from the same source as <see cref="Equals(MPath?)" /> -- consistency is structural.
    /// </summary>
    public override int GetHashCode() => HostOs.PathComparer.GetHashCode(_path);

    /// <summary>
    /// Lexicographic ordering consistent with <see cref="Equals(MPath?)" />. Null precedes any value.
    /// </summary>
    public int CompareTo(MPath? other)
    {
        if (ReferenceEquals(this, other))
            return 0;

        return other is null ? 1 : string.Compare(_path, other._path, HostOs.PathComparison);
    }

    /// <summary>
    /// Returns `true` when both paths are equal under <see cref="HostOs.PathComparer" /> semantics.
    /// </summary>
    public static bool operator ==(MPath? left, MPath? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// Returns `true` when the two paths are not equal under <see cref="HostOs.PathComparer" /> semantics.
    /// </summary>
    public static bool operator !=(MPath? left, MPath? right) => !(left == right);

    /// <summary>
    /// Returns `true` when `left` sorts before `right`.
    /// `null` sorts before any path.
    /// </summary>
    public static bool operator <(MPath? left, MPath? right) =>
            left is null ? right is not null : left.CompareTo(right) < 0;

    /// <summary>
    /// Returns `true` when `left` sorts before or equal to `right`.
    /// `null` sorts before any path.
    /// </summary>
    public static bool operator <=(MPath? left, MPath? right) => left is null || left.CompareTo(right) <= 0;

    /// <summary>
    /// Returns `true` when `left` sorts after `right`. Any path sorts
    /// after `null`.
    /// </summary>
    public static bool operator >(MPath? left, MPath? right) => left is not null && left.CompareTo(right) > 0;

    /// <summary>
    /// Returns `true` when `left` sorts after or equal to `right`.
    /// `null` is only greater-than-or-equal to `null`.
    /// </summary>
    public static bool operator >=(MPath? left, MPath? right) =>
            left is null ? right is null : left.CompareTo(right) >= 0;

#endregion

#region Navigation

    /// <summary>
    /// Parent directory. Throws <see cref="NoParentException" /> when this path IS the root.
    /// </summary>
    /// <exception cref="NoParentException">
    /// This path is a root.
    /// </exception>
    public MPath Parent => TryGetParent(out var parent) ? parent! : throw new NoParentException(this);

    /// <summary>
    /// Non-throwing parent lookup. Returns `false` at root.
    /// </summary>
    public bool TryGetParent(out MPath? parent)
    {
        if (_path == Root) {
            parent = null;

            return false;
        }

        var lastSlash = _path.LastIndexOf('/');
        var parentPath = lastSlash < Root.Length ? Root : _path[..lastSlash];
        parent = new(parentPath, Root); // substring of already-normalized path

        return true;
    }

    /// <summary>Walks `levels` levels up the parent chain. `Up(0)` returns this.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// `levels` is negative.
    /// </exception>
    /// <exception cref="NoParentException">
    /// `levels` exceeds <see cref="Depth" />.
    /// </exception>
    public MPath Up(int levels)
    {
        if (levels < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(levels),
                levels,
                $"Up() requires a non-negative count. Received {levels}. Use Up(0) to return self; use Parent to walk up one level."
            );
        }

        var depth = Depth;
        if (levels > depth) {
            throw new NoParentException(
                this,
                $"Cannot go up {levels} levels from '{_path}' (depth={depth}). Maximum ascent is {depth}."
            );
        }

        var current = this;
        for (var i = 0; i < levels; i++) {
            current = current.Parent;
        }

        return current;
    }

#endregion

#region Joining

    /// <summary>
    /// Joins a relative `segment` beneath `left` and re-normalizes.
    /// Leading separators on the RHS are stripped, so the RHS is always treated as relative.
    /// </summary>
    /// <remarks>
    /// The segment can contain additional separators and `..`. After normalization the result
    /// may escape `left`. For untrusted input, validate the result.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// `left` or `segment` is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// The joined path is not a valid absolute path.
    /// </exception>
    public static MPath operator /(MPath left, string segment)
    {
        Guard.NotNull(left, "Left-hand operand of the / operator cannot be null.");
        Guard.NotNull(
            segment,
            "Right-hand segment cannot be null. Pass an empty string to keep the base path unchanged."
        );

        var trimmed = segment.TrimStart('/', '\\');
        if (trimmed.Length == 0) {
            return left;
        }

        // Fast path: if the segment is a single lexical segment (no separators, not `.`/`..`),
        // validate it against the single-segment invariant and append directly. Covers the
        // overwhelmingly common case: `base / "filename.ext"`.
        if (CanUseSingleSegmentFastPath(trimmed.AsSpan())) {
            PathNormalizer.ValidSinglePathSegment(trimmed, "Segment");

            var fastPath = left._path.Length > 0 && left._path[^1] == '/'
                    ? left._path + trimmed
                    : left._path + "/" + trimmed;

            return new(fastPath, left.Root);
        }

        var combined = left._path + "/" + trimmed;
        var result = PathNormalizer.Normalize(combined, MPathOptions.Default);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Returns whether a segment can be appended directly without full re-normalization.
    /// </summary>
    /// <remarks>
    /// Fast-path segments are non-empty single segments with no separators and no `.` / `..`
    /// traversal semantics. Value validation still runs separately.
    /// </remarks>
    private static bool CanUseSingleSegmentFastPath(ReadOnlySpan<char> segment)
    {
        if (segment.IsEmpty) {
            return false;
        }

        if (segment is "." or "..") {
            return false;
        }

        return segment.IndexOfAny(DirectorySeparators) < 0;
    }

    /// <summary>
    /// Joins `segments` beneath this path in order. Normalizes once at the end.
    /// Null or empty segments are skipped.
    /// </summary>
    public MPath Join(params string[]? segments)
    {
        if (segments is null || segments.Length == 0) {
            return this;
        }

        // Short-circuit: one valid segment is just `this / segment` (which has its own fast path).
        if (segments.Length == 1) {
            return string.IsNullOrEmpty(segments[0]) ? this : this / segments[0];
        }

        var sb = new StringBuilder(_path);
        var any = false;
        foreach (var seg in segments) {
            if (string.IsNullOrEmpty(seg)) {
                continue;
            }

            any = true;
            sb.Append('/').Append(seg.TrimStart('/', '\\'));
        }

        if (!any) {
            return this;
        }

        var result = PathNormalizer.Normalize(sb.ToString(), MPathOptions.Default);

        return new(result.Normalized, result.Root);
    }

#endregion

#region Containment

    /// <summary>
    /// Resolves `segment` against this path and verifies the result does not
    /// escape via `..` traversal. Use this to safely join user-supplied path fragments
    /// (archive entries, HTTP filenames, config values) under a trusted base.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This is a <strong>lexical</strong> check -- it operates on the normalized string form
    ///     and does not touch the filesystem. It rejects `../etc/passwd`-style escape and also
    ///     rejects sibling-prefix false positives (e.g. `/var/www-evil` is NOT contained in
    ///     `/var/www`). Leading directory separators on `segment` are stripped
    ///     before resolution, so the segment is always treated as relative to this path --
    ///     consistent with the `/` operator.
    ///     </para>
    ///     <para>
    ///     This is <strong>NOT</strong> a security boundary for attacker-controlled filesystems.
    ///     It does NOT resolve symlinks, does NOT prevent TOCTOU races, and does NOT handle
    ///     NTFS per-directory case-sensitivity divergence. If the filesystem contains
    ///     attacker-placed symlinks (multi-tenant servers, untrusted archive extraction,
    ///     attacker-supplied Docker volumes), keep this method as the lexical containment
    ///     check only. Enforce symlink-aware policy in the I/O layer or platform API that
    ///     actually opens or creates the file.
    ///     </para>
    ///     <para>
    ///     Use <see cref="TryResolveContained" /> in hot paths (ZIP extraction loops, per-request
    ///     filename validation) to avoid the exception-allocation cost when rejection is common.
    ///     </para>
    /// </remarks>
    /// <param name="segment">
    /// Relative path fragment to append under this path. Leading `/` or `\` is stripped.
    /// </param>
    /// <returns>
    /// A new <see cref="MPath" /> that is either equal to this path or a descendant of it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// `segment` is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// `segment` is empty, whitespace-only, or resolves to no change
    /// (bare `.` or `..`); contains invalid characters; or normalizes to a path above this
    /// path's boundary.
    /// </exception>
    [Pure]
    public MPath ResolveContained(string segment)
    {
        Guard.NotNull(
            segment,
            "Segment cannot be null. Pass a non-null relative path fragment; use TryResolveContained for empty/invalid input handling."
        );

        if (!TryResolveContainedCore(segment, out var result, out var failureReason)) {
            throw new InvalidPathException(segment, failureReason!);
        }

        return result!;
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="ResolveContained" />. Returns
    /// `false` for any reason <see cref="ResolveContained" /> would throw
    /// <see cref="InvalidPathException" />, including empty/pure-dots input, invalid
    /// characters, and lexical escape.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Recommended for hot paths -- ZIP extraction loops, per-request filename checks,
    ///     bulk validation. Avoids the allocation cost of constructing an
    ///     <see cref="InvalidPathException" /> on rejection.
    ///     </para>
    ///     <para>
    ///     Only <see cref="ArgumentNullException" /> escapes this method. Null
    ///     `segment` stays loud, mirroring <see cref="TryFrom" />'s contract.
    ///     All other failure modes return `false` with `result`
    ///     set to `null`.
    ///     </para>
    /// </remarks>
    /// <param name="segment">
    /// Relative path fragment to append under this path. Leading `/` or `\` is stripped.
    /// </param>
    /// <param name="result">
    /// On success, the resolved contained <see cref="MPath" />; on failure,
    /// `null`.
    /// </param>
    /// <returns>
    /// `true` when `segment` resolves to a path contained
    /// under this path; `false` otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// `segment` is null.
    /// </exception>
    public bool TryResolveContained(string segment, [NotNullWhen(true)] out MPath? result)
    {
        Guard.NotNull(
            segment,
            "Segment cannot be null. Pass a non-null relative path fragment; use TryResolveContained for empty/invalid input handling."
        );

        return TryResolveContainedCore(segment, out result, out _);
    }

    /// <summary>
    /// Validates and resolves a contained segment without allocating an exception on failure.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="ResolveContained(string)" /> and
    /// <see cref="TryResolveContained(string, out MPath?)" /> so both methods use the same
    /// validation, normalization, and failure-reason logic.
    /// </remarks>
    private bool TryResolveContainedCore(
        string segment,
        out MPath? result,
        out string? failureReason
    )
    {
        // Strip one leading separator so absolute-looking input still resolves under the base.
        var stripped = segment.Length > 0 && segment[0] is '/' or '\\'
                ? segment[1..]
                : segment;

        // Reject empty or no-op targets. Callers should name a concrete child path.
        if (string.IsNullOrWhiteSpace(stripped) || stripped is "." or "..") {
            result = null;
            failureReason = "Segment is empty or resolves to no change ('', '.', '..' alone are not valid targets)";

            return false;
        }

        // Normalize relative to the current path and reuse PathNormalizer's validation.
        NormalizationResult normalized;
        try {
            normalized = PathNormalizer.Normalize(stripped, MPathOptions.Default, _path);
        } catch (InvalidPathException ex) {
            // Preserve PathNormalizer's human-readable reason without allocating a second
            // exception for TryResolveContained.
            result = null;
            failureReason = ex.Reason;

            return false;
        }

        // Containment is lexical: the normalized result must stay at or below the base path.
        if (!IsContained(_path, normalized.Normalized)) {
            result = null;
            failureReason =
                    $"Segment normalizes above the base path '{_path}'. Use a segment that stays within the boundary";

            return false;
        }

        result = new(normalized.Normalized, normalized.Root);
        failureReason = null;

        return true;
    }

    /// <summary>
    /// Returns whether a normalized candidate path is equal to or contained under a normalized ancestor path.
    /// </summary>
    /// <remarks>
    /// This is a lexical check on normalized forward-slash paths. Exact equality is allowed;
    /// otherwise the candidate must continue with `/` so sibling-prefix matches like
    /// `/var/www-evil` do not count as contained. This check does not resolve symlinks.
    /// </remarks>
    /// <example>
    ///     <code>
    /// IsContained("/var/www", "/var/www"); // true
    /// IsContained("/var/www", "/var/www/uploads/file.txt"); // true
    /// IsContained("/var/www", "/var/www-evil/file.txt"); // false
    ///     </code>
    /// </example>
    private static bool IsContained(string ancestor, string candidate)
    {
        if (candidate.Length < ancestor.Length) {
            return false;
        }

        if (!candidate.AsSpan(0, ancestor.Length).Equals(ancestor.AsSpan(), HostOs.PathComparison)) {
            return false;
        }

        // Exact equality is valid.
        if (candidate.Length == ancestor.Length) {
            return true;
        }

        // Require a path-segment boundary after the ancestor prefix.
        return candidate[ancestor.Length] == '/';
    }

#endregion

#region Mutation

    /// <summary>
    /// Returns a new <see cref="MPath" /> with the final segment replaced by `name`.
    /// The replacement must be a valid single path segment: non-empty, no directory separators,
    /// not `.` or `..`, free of platform-invalid segment characters, and on Windows not a
    /// reserved device name or a segment ending in `.` / space.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// `name` is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// `name` is empty, contains a directory separator, is `.` or `..`,
    /// contains platform-invalid segment characters, is a Windows-reserved device name,
    /// ends with `.` / space on Windows, or this path is a root.
    /// </exception>
    public MPath WithName(string name)
    {
        Guard.NotNull(name, "Name cannot be null. Pass a non-empty segment string.");

        // Exception wording is public API; preserve the pre-Task-2 empty-name message.
        if (name.Length == 0) {
            throw new InvalidPathException(name, "Name is empty. Pass a non-empty segment string");
        }

        PathNormalizer.ValidSinglePathSegment(name, "Name");

        if (_path == Root) {
            throw new InvalidPathException(
                _path,
                "Cannot set a name on a root path. Root paths have no final segment to replace"
            );
        }

        var lastSlash = _path.LastIndexOf('/');
        var parentPath = lastSlash < Root.Length ? Root : _path[..lastSlash];
        // Parent is already normalized, name is validated, so no re-normalization needed.
        var combined = parentPath.EndsWith('/') ? parentPath + name : parentPath + "/" + name;

        return new(combined, Root);
    }

    /// <summary>
    /// Returns a new <see cref="MPath" /> with the file extension replaced. Leading dot is optional;
    /// null or empty string removes the extension. After combination with the current file name,
    /// the resulting segment must still satisfy the same single-segment invariants as
    /// <see cref="WithName(string)" />.
    /// </summary>
    /// <exception cref="InvalidPathException">
    /// `extension` is a bare "."; contains a directory separator; or,
    /// after combination with the current file name, produces an invalid single segment
    /// (for example `.` / `..`, a platform-invalid character, a Windows-reserved device name,
    /// trailing `.` / space on Windows, or any invalid name on a root path).
    /// </exception>
    public MPath WithExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) {
            return WithoutExtension();
        }

        if (extension == ".") {
            throw new InvalidPathException(
                extension,
                "Extension '.' is not valid. Use WithoutExtension() to remove the extension."
            );
        }

        var ext = extension[0] == '.' ? extension : "." + extension;
        if (ext.IndexOfAny(DirectorySeparators) >= 0) {
            throw new InvalidPathException(
                ext,
                $"Extension '{ext}' contains a directory separator. Extensions cannot span directories; use WithName to change the file segment."
            );
        }

        return WithName(NameWithoutExtension + ext);
    }

    /// <summary>
    /// Returns a new <see cref="MPath" /> with any trailing extension stripped. Idempotent.
    /// </summary>
    public MPath WithoutExtension() => HasExtension ? WithName(NameWithoutExtension) : this;

#endregion
}
