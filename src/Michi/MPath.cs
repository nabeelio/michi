using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using Michi.Internal;

namespace Michi;

/// <summary>
/// A normalized, immutable absolute filesystem path. Equality is host-OS aware.
/// </summary>
/// <remarks>
/// Construct via <see cref="From(string, MPathOptions?)" /> or <see cref="TryFrom(string?, out MPath?, MPathOptions?)" />.
/// See
/// <c>
/// README.md
/// </c>
/// for a cookbook; see tests under
/// <c>
/// tests/MPath.Tests/
/// </c>
/// for
/// exhaustive examples of each API member.
/// </remarks>
[DebuggerDisplay("{_path,nq}")]
[JsonConverter(typeof(MPathJsonConverter))]
[TypeConverter(typeof(MPathTypeConverter))]
[SuppressMessage(
    "Design",
    "CA1036:Override methods on comparable types",
    Justification =
            "Comparison operators (<, <=, >, >=) land in plan 01-04b alongside the full IComparable implementation."
)]
public sealed class MPath : IEquatable<MPath>, IComparable<MPath> {
    private readonly string _osNativePath;

    // Storage: forward-slash canonical form + precomputed OS-native form.
    // Both are computed once in the private constructor; the class is immutable.
    private readonly string _path;
    private readonly string[] _segments;

    /// <summary>
    /// Private constructor. Called only from <see cref="From(string, MPathOptions?)" /> (and overloads)
    /// after <see cref="Internal.PathNormalizer.Normalize(string, MPathOptions, string?)" /> has validated + normalized the
    /// input.
    /// </summary>
    /// <param name="normalizedPath">
    /// Forward-slash, absolute, no trailing slash unless root.
    /// </param>
    /// <param name="root">
    /// Root token as extracted by the normalizer.
    /// </param>
    private MPath(string normalizedPath, string root)
    {
        _path = normalizedPath;
        Root = root;
        _osNativePath = HostOs.IsWindows ? _path.Replace('/', '\\') : _path;

        // Derive segments once.
        if (_path == root) {
            _segments = Array.Empty<string>();
            Name = string.Empty;
            NameWithoutExtension = string.Empty;
            Extension = string.Empty;
            HasExtension = false;
            Depth = 0;
        } else {
            // Skip the root, then split remainder on '/'.
            var belowRoot = _path[root.Length..];
            if (belowRoot.Length > 0 && belowRoot[0] == '/') {
                belowRoot = belowRoot[1..];
            }

            _segments = belowRoot.Split('/');
            Depth = _segments.Length;

            var last = _segments[^1];
            Name = last;

            // Extension detection: rightmost '.' that is NOT the first character of the name.
            var dotIndex = last.LastIndexOf('.');
            if (dotIndex > 0) {
                NameWithoutExtension = last.Substring(0, dotIndex);
                Extension = last.Substring(dotIndex);
                HasExtension = true;
            } else {
                NameWithoutExtension = last;
                Extension = string.Empty;
                HasExtension = false;
            }
        }
    }

    /// <summary>
    /// Root token of this path (forward-slash form). "/" on Unix; "C:/" on Windows drive paths; "//server/share" for UNC.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// The final segment (file or directory name). Empty string if this path is the root.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The final segment without its extension. For "foo.txt" returns "foo"; for "foo" returns "foo"; for ".bashrc" returns
    /// ".bashrc" (leading dot is NOT treated as an extension separator).
    /// </summary>
    public string NameWithoutExtension { get; }

    /// <summary>
    /// The extension including the leading dot (e.g. ".txt"), or empty string if none. For ".bashrc" returns "" (leading dot
    /// on a hidden file is not an extension).
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Whether <see cref="Extension" /> is non-empty.
    /// </summary>
    public bool HasExtension { get; }

    /// <summary>
    /// Number of segments below the root. Root itself has depth 0; "/a" has depth 1; "/a/b/c" has depth 3.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Path segments below the root, in order. Returns a defensive copy — the caller may mutate the returned array without
    /// affecting this instance.
    /// </summary>
    public string[] Segments => (string[]) _segments.Clone();

#region Construction API (CORE-02)

    /// <summary>
    /// Normalizes <paramref name="path" /> and returns an <see cref="MPath" />.
    /// If <paramref name="path" /> is relative, it is resolved against
    /// <see cref="MPathOptions.BaseDirectory" /> from <paramref name="options" />
    /// (or <see cref="MPathOptions.Default" />).
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// Path cannot be normalized to a valid absolute path (empty, contains invalid characters, references an undefined
    /// environment variable, etc.).
    /// </exception>
    public static MPath From(string path, MPathOptions? options = null)
    {
        if (path is null) {
            throw new ArgumentNullException(
                nameof(path),
                "Path cannot be null. Use TryFrom to accept null input without exceptions."
            );
        }

        var opts = options ?? MPathOptions.Default;
        var result = PathNormalizer.Normalize(path, opts);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Normalizes <paramref name="path" /> using <paramref name="relativeTo" /> as the base for relative-path resolution
    /// (overriding <see cref="MPathOptions.BaseDirectory" />).
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path" /> or <paramref name="relativeTo" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// Path cannot be normalized to a valid absolute path.
    /// </exception>
    public static MPath From(string path, string relativeTo, MPathOptions? options = null)
    {
        if (path is null) {
            throw new ArgumentNullException(
                nameof(path),
                "Path cannot be null. Use TryFrom to accept null input without exceptions."
            );
        }

        if (relativeTo is null) {
            throw new ArgumentNullException(
                nameof(relativeTo),
                "relativeTo cannot be null. Pass an absolute path string to resolve relative inputs against."
            );
        }

        var opts = options ?? MPathOptions.Default;
        var result = PathNormalizer.Normalize(path, opts, relativeTo);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Non-throwing construction. Returns <see langword="true" /> and sets <paramref name="result" />
    /// if <paramref name="path" /> is normalizable; returns <see langword="false" /> (with <paramref name="result" /> set to
    /// null) otherwise.
    /// </summary>
    /// <remarks>
    /// Accepts null input without throwing. Any <see cref="InvalidPathException" /> from the normalization pipeline is caught
    /// and converted to a false return.
    /// </remarks>
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
    /// Formats <paramref name="template" /> with <paramref name="args" /> using
    /// <see cref="string.Format(IFormatProvider, string, object?[])" /> semantics
    /// (double braces for literals:
    /// <c>
    /// "{{"
    /// </c>
    /// and
    /// <c>
    /// "}}"
    /// </c>
    /// ), then constructs an <see cref="MPath" />
    /// from the result. Uses <see cref="System.Globalization.CultureInfo.InvariantCulture" />
    /// so numeric/date formatting is stable across consumer locales.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <strong>
    ///     SECURITY:
    ///     </strong>
    ///     Unvetted user input in <paramref name="args" /> can cause traversal
    ///     (
    ///     <c>
    ///     ..
    ///     </c>
    ///     ) when substituted. For untrusted input, compose via
    ///     <c>
    ///     base / segment
    ///     </c>
    ///     (which
    ///     strips leading separators) or use
    ///     <c>
    ///     ResolveContained
    ///     </c>
    ///     (Phase 2) for an explicit containment guarantee.
    ///     </para>
    ///     <para>
    ///     Characters rejected by <see cref="Path.GetInvalidPathChars" /> on the current platform will
    ///     cause <see cref="InvalidPathException" />. On Windows this rejects
    ///     <c>
    ///     &lt;
    ///     </c>
    ///     ,
    ///     <c>
    ///     &gt;
    ///     </c>
    ///     ,
    ///     <c>
    ///     "
    ///     </c>
    ///     ,
    ///     <c>
    ///     |
    ///     </c>
    ///     ,
    ///     <c>
    ///     ?
    ///     </c>
    ///     ,
    ///     <c>
    ///     *
    ///     </c>
    ///     , and control characters. On Unix this rejects only
    ///     <c>
    ///     \0
    ///     </c>
    ///     .
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="template" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// Substituted result contains an invalid path character or cannot be normalized.
    /// </exception>
    /// <exception cref="FormatException">
    /// <paramref name="template" /> is malformed (propagated from
    /// <see cref="string.Format(IFormatProvider, string, object?[])" />).
    /// </exception>
    public static MPath Format(string template, params object?[] args)
    {
        if (template is null) {
            throw new ArgumentNullException(
                nameof(template),
                "Format template cannot be null. Provide a string.Format-style template."
            );
        }

        var substituted = string.Format(CultureInfo.InvariantCulture, template, args ?? Array.Empty<object?>());

        // Invalid-char validation BEFORE normalization so we can name the offending character in the error.
        var invalid = Path.GetInvalidPathChars();
        for (var i = 0; i < substituted.Length; i++) {
            var ch = substituted[i];
            for (var j = 0; j < invalid.Length; j++) {
                if (ch == invalid[j]) {
                    throw new InvalidPathException(
                        substituted,
                        $"Contains invalid path character '{(char.IsControl(ch) ? ' ' : ch)}' (0x{(int) ch:X4}) at index {i}. See Path.GetInvalidPathChars() for the current platform ({(HostOs.IsWindows ? "Windows" : "Unix")})."
                    );
                }
            }
        }

        return From(substituted);
    }

#endregion

#region String output (CORE-05)

    /// <summary>
    /// OS-native separator form. "/foo/bar" on Unix; "\foo\bar" on Windows.
    /// </summary>
    public override string ToString()
    {
        return _osNativePath;
    }

    /// <summary>
    /// Forward-slash form, portable across platforms. "/foo/bar" on every OS.
    /// </summary>
    public string ToUnixString()
    {
        return _path;
    }

    /// <summary>
    /// Backslash form. "\foo\bar" on every OS. Useful when targeting Windows-specific APIs from a non-Windows build
    /// environment.
    /// </summary>
    public string ToWindowsString()
    {
        return _path.Replace('/', '\\');
    }

    /// <summary>
    /// Explicit string cast. Equivalent to <see cref="ToString" />. Returns null for null input (standard C# cast-null
    /// semantics).
    /// </summary>
    public static explicit operator string(MPath path)
    {
        return path is null ? null! : path.ToString();
    }

#endregion

#region Well-known paths (WELL-01)

    /// <summary>
    /// The current user's home directory (<see cref="Environment.SpecialFolder.UserProfile" />). Computed once at first
    /// access.
    /// </summary>
    public static MPath Home => WellKnownPaths.Home.Value;

    /// <summary>
    /// The system temporary directory (<see cref="Path.GetTempPath" />). Computed once at first access.
    /// </summary>
    public static MPath Temp => WellKnownPaths.Temp.Value;

    /// <summary>
    /// The process current working directory. Evaluated on every access — not cached, because the process CWD can change.
    /// </summary>
    public static MPath CurrentDirectory => From(Directory.GetCurrentDirectory());

#endregion

#region Equality, comparison, operators (CORE-06)

    /// <summary>
    /// Value equality by normalized path using <see cref="HostOs.PathComparison" />:
    /// <see cref="StringComparison.OrdinalIgnoreCase" /> on Windows and macOS,
    /// <see cref="StringComparison.Ordinal" /> on Linux. Use <see cref="MPathComparer" />
    /// for explicit comparer semantics independent of the host OS.
    /// </summary>
    public bool Equals(MPath? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return string.Equals(_path, other._path, HostOs.PathComparison);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is MPath other && Equals(other);
    }

    /// <summary>
    /// Hash code derived from <see cref="HostOs.PathComparer" /> — the SAME source as
    /// <see cref="Equals(MPath?)" />. Consistency is structural, not accidental.
    /// </summary>
    public override int GetHashCode()
    {
        return HostOs.PathComparer.GetHashCode(_path);
    }

    /// <summary>
    /// Lexicographic ordering by normalized path using <see cref="HostOs.PathComparison" />.
    /// Produces a total order consistent with <see cref="Equals(MPath?)" />.
    /// Null precedes any value.
    /// </summary>
    public int CompareTo(MPath? other)
    {
        if (ReferenceEquals(this, other))
            return 0;

        if (other is null)
            return 1;

        return string.Compare(_path, other._path, HostOs.PathComparison);
    }

    /// <summary>
    /// Reference-safe equality. Delegates to <see cref="Equals(MPath?)" />.
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
    /// Logical negation of <see cref="operator ==(MPath?, MPath?)" />.
    /// </summary>
    public static bool operator !=(MPath? left, MPath? right)
    {
        return !(left == right);
    }

#endregion

#region Navigation (NAV-01)

    /// <summary>
    /// Parent directory. Throws <see cref="NoParentException" /> when this path IS the root.
    /// </summary>
    /// <exception cref="NoParentException">
    /// This path is a root (has no parent).
    /// </exception>
    public MPath Parent {
        get {
            if (TryGetParent(out var parent)) {
                return parent!;
            }

            throw new NoParentException(this);
        }
    }

    /// <summary>
    /// Non-throwing parent lookup. Returns <see langword="false" /> if this path is a root.
    /// </summary>
    public bool TryGetParent(out MPath? parent)
    {
        if (_path == Root) {
            parent = null;

            return false;
        }

        // Strip the last segment; the normalized form uses `/` so LastIndexOf is correct regardless of OS.
        var lastSlash = _path.LastIndexOf('/');
        // If lastSlash points inside the root prefix, parent IS the root.
        // Example: _path = "/foo", Root = "/", lastSlash = 0 → parent path is "/".
        var parentPath = lastSlash < Root.Length ? Root : _path.Substring(0, lastSlash);
        if (parentPath.Length == 0) {
            parentPath = Root; // defensive: never emit an empty string
        }

        var result = PathNormalizer.Normalize(parentPath, MPathOptions.Default);
        parent = new(result.Normalized, result.Root);

        return true;
    }

    /// <summary>
    /// Walks <paramref name="levels" /> levels up the parent chain.
    /// <c>
    /// Up(0)
    /// </c>
    /// returns this instance.
    /// </summary>
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

        if (levels > Depth) {
            throw new NoParentException(
                this,
                $"Cannot go up {levels} levels from '{_path}' (depth={Depth}). Maximum ascent is {Depth}."
            );
        }

        var current = this;
        for (var i = 0; i < levels; i++) {
            current = current.Parent; // safe — we bounded by Depth
        }

        return current;
    }

#endregion

#region Joining (CORE-04)

    /// <summary>
    /// Joins a relative <paramref name="segment" /> beneath <paramref name="left" /> and re-normalizes.
    /// Leading directory separators on <paramref name="segment" /> are STRIPPED before joining — the RHS
    /// is always treated as relative. (
    /// <c>
    /// base / "/etc"
    /// </c>
    /// returns
    /// <c>
    /// base/etc
    /// </c>
    /// , not
    /// <c>
    /// /etc
    /// </c>
    /// .)
    /// </summary>
    /// <remarks>
    /// The segment MAY contain additional separators (
    /// <c>
    /// "foo/bar"
    /// </c>
    /// ) and MAY contain
    /// <c>
    /// ..
    /// </c>
    /// .
    /// Use
    /// <c>
    /// ResolveContained
    /// </c>
    /// (Phase 2) if you need to guarantee the result stays under
    /// <paramref name="left" />.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="left" /> or <paramref name="segment" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// The joined and re-normalized path is not a valid absolute path.
    /// </exception>
    public static MPath operator /(MPath left, string segment)
    {
        if (left is null) {
            throw new ArgumentNullException(
                nameof(left),
                "Left-hand operand of the / operator cannot be null. Construct an MPath via From before joining."
            );
        }

        if (segment is null) {
            throw new ArgumentNullException(
                nameof(segment),
                "Right-hand segment of the / operator cannot be null. Pass an empty string to keep the base path unchanged."
            );
        }

        // D-16a: RHS-always-relative. Strip leading directory separators.
        var trimmed = segment.TrimStart('/', '\\');
        if (trimmed.Length == 0) {
            return left;
        }

        // Join and re-normalize through the full pipeline so `..`, `.`, duplicate separators,
        // and OS-native backslashes in `segment` are all handled consistently.
        var combined = left._path + "/" + trimmed;
        var result = PathNormalizer.Normalize(combined, MPathOptions.Default);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Joins <paramref name="segments" /> beneath this path, in order. Null or empty segments are skipped.
    /// Each segment follows the same rules as <see cref="operator /(MPath, string)" /> — leading separators stripped,
    /// internal separators allowed,
    /// <c>
    /// ..
    /// </c>
    /// allowed.
    /// </summary>
    /// <remarks>
    /// Returns this instance if <paramref name="segments" /> is null, empty, or contains only null/empty entries.
    /// </remarks>
    public MPath Join(params string[] segments)
    {
        if (segments is null || segments.Length == 0) {
            return this;
        }

        var current = this;
        for (var i = 0; i < segments.Length; i++) {
            var seg = segments[i];
            if (string.IsNullOrEmpty(seg)) {
                continue;
            }

            current = current / seg;
        }

        return current;
    }

#endregion

#region Mutation (MUT-01)

    /// <summary>
    /// Returns a new <see cref="MPath" /> with the final segment replaced by <paramref name="name" />.
    /// The name must NOT contain directory separators — use <see cref="operator /(MPath, string)" /> or
    /// <see cref="Join(string[])" /> for multi-segment composition.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name" /> is null.
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// <paramref name="name" /> is empty or contains a directory separator.
    /// </exception>
    public MPath WithName(string name)
    {
        if (name is null) {
            throw new ArgumentNullException(
                nameof(name),
                "Name cannot be null. Pass a non-empty segment string; use WithoutExtension / WithExtension for extension-only changes."
            );
        }

        if (name.Length == 0) {
            throw new InvalidPathException(
                name,
                "Name is empty. Pass a non-empty segment string"
            );
        }

        if (name.Contains('/') || name.Contains('\\')) {
            throw new InvalidPathException(
                name,
                $"Name '{name}' contains a directory separator. Use the / operator or Join() to compose multi-segment paths."
            );
        }

        // If this path is a root, WithName is meaningless.
        if (_path == Root) {
            throw new InvalidPathException(
                _path,
                "Cannot set a name on a root path. Root paths have no final segment to replace"
            );
        }

        var lastSlash = _path.LastIndexOf('/');
        var parent = lastSlash < Root.Length ? Root : _path.Substring(0, lastSlash);
        var combined = parent.EndsWith('/') ? parent + name : parent + "/" + name;
        var result = PathNormalizer.Normalize(combined, MPathOptions.Default);

        return new(result.Normalized, result.Root);
    }

    /// <summary>
    /// Returns a new <see cref="MPath" /> with the file extension replaced by <paramref name="extension" />.
    /// The leading dot is optional:
    /// <c>
    /// WithExtension("txt")
    /// </c>
    /// and
    /// <c>
    /// WithExtension(".txt")
    /// </c>
    /// are equivalent.
    /// Passing <see langword="null" /> or empty string removes the extension (equivalent to <see cref="WithoutExtension" />).
    /// </summary>
    /// <exception cref="InvalidPathException">
    /// <paramref name="extension" /> is a bare
    /// <c>
    /// "."
    /// </c>
    /// .
    /// </exception>
    public MPath WithExtension(string? extension)
    {
        if (extension is null || extension.Length == 0) {
            return WithoutExtension();
        }

        if (extension == ".") {
            throw new InvalidPathException(
                extension,
                "Extension '.' is not valid. Use WithoutExtension() to remove the extension."
            );
        }

        // Normalize the extension to include exactly one leading dot.
        var ext = extension[0] == '.' ? extension : "." + extension;

        // Reject separators inside the extension.
        if (ext.Contains('/') || ext.Contains('\\')) {
            throw new InvalidPathException(
                ext,
                $"Extension '{ext}' contains a directory separator. Extensions cannot span directories; use WithName to change the file segment."
            );
        }

        return WithName(NameWithoutExtension + ext);
    }

    /// <summary>
    /// Returns a new <see cref="MPath" /> with any trailing extension stripped. Idempotent on paths without an extension.
    /// </summary>
    public MPath WithoutExtension()
    {
        if (!HasExtension) {
            return this;
        }

        return WithName(NameWithoutExtension);
    }

#endregion
}
