using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
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
/// <see cref="Path" /> property both return the OS-native-separator form (backslash
/// on Windows, forward-slash elsewhere) so paths drop into `System.IO` and most
/// third-party string-typed APIs without extra conversion. Use <see cref="ToUnixString" />
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
    // The OS-native form is stored as the Path auto-property below and assigned once
    // in the ctor so ToString()/Path are O(1) allocation-free per CORE-05 + D-03.
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
        Path = HostOs.IsWindows ? normalizedPath.Replace('/', '\\') : normalizedPath;
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
    /// Normalizes <paramref name="path" /> and returns an <see cref="MPath" />.
    /// Relative inputs are resolved against <see cref="MPathOptions.BaseDirectory" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path" /> is null.
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
    /// Normalizes <paramref name="path" /> using <paramref name="relativeTo" /> as the base
    /// for relative-path resolution (overriding <see cref="MPathOptions.BaseDirectory" />).
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path" /> or <paramref name="relativeTo" /> is null.
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
    /// Non-throwing construction. Returns <see langword="true" /> and sets <paramref name="result" />
    /// if <paramref name="path" /> is normalizable; returns <see langword="false" /> with null result otherwise.
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
    /// Substitutes <paramref name="args" /> into <paramref name="template" /> via
    /// <see cref="string.Format(IFormatProvider, string, object?[])" /> and constructs an
    /// <see cref="MPath" />. Uses <see cref="CultureInfo.InvariantCulture" /> so numeric and
    /// date formatting is locale-stable.
    /// </summary>
    /// <remarks>
    /// <strong>Security:</strong> unvetted user input in <paramref name="args" /> can inject
    /// `..` segments that escape the intended base. For untrusted input, compose via the
    /// `/` operator (which strips leading separators) and validate the result.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="template" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// The substituted result is not a valid path.
    /// </exception>
    /// <exception cref="FormatException">
    /// <paramref name="template" /> is malformed.
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
    /// <see cref="ToUnixString" /> instead. The <see cref="Path" /> property returns the
    /// same value as this method and is preferred in LINQ projections and data binding.
    /// </remarks>
    public override string ToString() => Path;

    /// <summary>
    /// The path as a string, in OS-native separator form. Equivalent to <see cref="ToString" />;
    /// provided as a property for LINQ projections, data binding, and pass-through to
    /// string-typed APIs.
    /// </summary>
    /// <example>
    ///     <code>
    /// File.ReadAllText(myPath.Path);
    /// var names = paths.Select(p => p.Path);
    /// </code>
    /// </example>
    public string Path { get; }

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

    public static bool operator ==(MPath? left, MPath? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(MPath? left, MPath? right) => !(left == right);

    public static bool operator <(MPath? left, MPath? right) =>
            left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(MPath? left, MPath? right) => left is null || left.CompareTo(right) <= 0;

    public static bool operator >(MPath? left, MPath? right) => left is not null && left.CompareTo(right) > 0;

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
    /// Non-throwing parent lookup. Returns <see langword="false" /> at root.
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

    /// <summary>Walks <paramref name="levels" /> levels up the parent chain. `Up(0)` returns this.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="levels" /> is negative.
    /// </exception>
    /// <exception cref="NoParentException">
    /// <paramref name="levels" /> exceeds <see cref="Depth" />.
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
    /// Joins a relative <paramref name="segment" /> beneath <paramref name="left" /> and re-normalizes.
    /// Leading separators on the RHS are stripped, so the RHS is always treated as relative.
    /// </summary>
    /// <remarks>
    /// The segment can contain additional separators and `..`. After normalization the result
    /// may escape <paramref name="left" />. For untrusted input, validate the result.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="left" /> or <paramref name="segment" /> is null.
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

        // Fast path: if the segment has no internal separators, no `.` or `..` components, and
        // no invalid path chars, we can skip the full normalizer and just append. The left side
        // is already canonical, the segment is safe, so the result is canonical by construction.
        // Covers the overwhelmingly common case: `base / "filename.ext"`.
        if (IsSimpleSegment(trimmed.AsSpan())) {
            var fastPath = left._path.Length > 0 && left._path[^1] == '/'
                    ? left._path + trimmed
                    : left._path + "/" + trimmed;

            return new(fastPath, left.Root);
        }

        var combined = left._path + "/" + trimmed;
        var result = PathNormalizer.Normalize(combined, MPathOptions.Default);

        return new(result.Normalized, result.Root);
    }

    // A "simple segment" is one where straight concatenation produces a canonical path:
    // no directory separators, no `.` / `..` traversal components, no invalid chars.
    private static bool IsSimpleSegment(ReadOnlySpan<char> segment)
    {
        // Empty / pure-dots rejected: "." and ".." need normalization.
        if (segment.IsEmpty) {
            return false;
        }

        if (segment is "." or "..") {
            return false;
        }

        foreach (var c in segment) {
            if (c is '/' or '\\') {
                return false;
            }

            // Control chars are invalid in paths on every platform we target.
            if (c < 0x20) {
                return false;
            }
        }

        // Invalid-char check against the platform's invalid-char set (Windows adds more).
        return segment.IndexOfAny(SysPath.GetInvalidFileNameChars()) < 0;
    }

    /// <summary>
    /// Joins <paramref name="segments" /> beneath this path in order. Normalizes once at the end.
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

#region Mutation

    /// <summary>
    /// Returns a new <see cref="MPath" /> with the final segment replaced by <paramref name="name" />.
    /// The name must not contain directory separators.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// <paramref name="name" /> is empty, contains a separator, or this path is a root.
    /// </exception>
    public MPath WithName(string name)
    {
        Guard.NotNull(name, "Name cannot be null. Pass a non-empty segment string.");

        if (name.Length == 0) {
            throw new InvalidPathException(name, "Name is empty. Pass a non-empty segment string");
        }

        if (name.IndexOfAny(DirectorySeparators) >= 0) {
            throw new InvalidPathException(
                name,
                $"Name '{name}' contains a directory separator. Use the / operator or Join() to compose multi-segment paths."
            );
        }

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
    /// null or empty string removes the extension.
    /// </summary>
    /// <exception cref="InvalidPathException">
    /// <paramref name="extension" /> is a bare ".".
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
