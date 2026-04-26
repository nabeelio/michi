using System.Diagnostics.CodeAnalysis;
using Segments.Exceptions;

namespace Segments.Internal;

/// <summary>
/// Canonical path + extracted root, returned by <see cref="PathNormalizer.Normalize" />.
/// </summary>
/// <param name="Normalized">
/// Normalized absolute path in forward-slash form. No trailing slash unless the path is the root
/// itself ("/" on Unix, "C:/" on Windows).
/// </param>
/// <param name="Root">
/// Root prefix in forward-slash form. "/" for Unix absolutes, "C:/" for Windows drive letters,
/// "//server/share" for UNC.
/// </param>
internal readonly record struct NormalizationResult(string Normalized, string Root);

/// <summary>
/// Canonicalizes paths: resolves `..`/`.` segments, collapses repeated separators, resolves
/// relative paths against a rooted base, extracts the root prefix.
/// </summary>
/// <remarks>
/// All the fragile work goes through <see cref="Path.GetFullPath(string, string)" />, two-arg overload
/// only. The single-arg form reads the mutable process CWD and is never used here.
/// </remarks>
internal static class PathNormalizer {
    private static readonly char[] DirectorySeparators = [
        '/',
        '\\',
    ];

    /// <summary>Normalizes `path` into a canonical absolute form.</summary>
    /// <exception cref="ArgumentNullException">`path` or `options` is null.</exception>
    /// <exception cref="InvalidPathException">
    /// Path is empty, can't resolve to absolute, or contains invalid characters.
    /// </exception>
    internal static NormalizationResult Normalize(string path, SPathOptions options, string? relativeTo = null)
    {
        Guard.NotNull(path);
        Guard.NotNull(options);

        var attempted = path;

        if (string.IsNullOrWhiteSpace(path)) {
            throw new InvalidPathException(
                attempted,
                "Path is empty. Provide a non-empty absolute or resolvable path string"
            );
        }

        if (options.ExpandEnvironmentVariables) {
            path = TildeExpander.ExpandEnvironmentVariables(path);
        }

        if (options.ExpandTilde) {
            path = TildeExpander.ExpandTilde(path);
        }

        if (string.IsNullOrWhiteSpace(path)) {
            throw new InvalidPathException(
                attempted,
                "Path is empty. Provide a non-empty absolute or resolvable path string"
            );
        }

        path = path.Replace('\\', '/');

        // Fully-qualified inputs ignore basePath, but rooted-relative inputs like `/foo` on
        // Windows still need the caller's base drive. Always pass a real fully-qualified base.
        string basePath;
        if (IsFullyQualified(path)) {
            basePath = AppContext.BaseDirectory;
        } else {
            basePath = relativeTo ?? options.BaseDirectory;
            if (string.IsNullOrWhiteSpace(basePath)) {
                throw new InvalidPathException(
                    attempted,
                    "Not a valid absolute path. Provide an absolute path, set SPathOptions.BaseDirectory, or pass a relativeTo argument"
                );
            }

            basePath = basePath.Replace('\\', '/');
            if (!IsFullyQualified(basePath)) {
                throw new InvalidPathException(
                    attempted,
                    "Not a valid absolute path. Provide an absolute path, set SPathOptions.BaseDirectory, or pass a relativeTo argument"
                );
            }
        }

        // Delegate ../., repeated separators, UNC root preservation, Windows drive-relative resolution
        // to the BCL. Two-arg overload only.
        string resolved;
        try {
            resolved = Path.GetFullPath(path, basePath);
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new InvalidPathException(attempted, $"Cannot resolve path: {ex.Message}", ex);
        }

        // Canonicalize to forward-slash and extract the root (BCL keeps UNC prefixes intact).
        var normalized = resolved.Replace('\\', '/');
        var root = (Path.GetPathRoot(resolved) ?? string.Empty).Replace('\\', '/');

        // Strip trailing slash unless the result IS the root (Windows may return "C:\foo\").
        if (normalized.Length > root.Length && normalized[^1] == '/') {
            normalized = normalized.TrimEnd('/');
            if (normalized.Length < root.Length) {
                normalized = root;
            }
        }

        // Validate the normalized tail. Unix-like hosts still use a flat invalid-char scan;
        // Windows walks segments once so invalid characters and segment-shape rules share one
        // post-normalization pipeline.
        ValidateNormalizedPath(normalized, root, attempted);

        return new(normalized, root);
    }

    private static bool IsFullyQualified(string path)
    {
        #if NET6_0_OR_GREATER
        return Path.IsPathFullyQualified(path);
        #else
        if (!HostOs.IsWindows) {
            return path.Length > 0 && path[0] == '/';
        }

        if (path.Length >= 3 && IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/') {
            return true;
        }

        if (path.Length < 2 || path[0] != '/' || path[1] != '/') {
            return false;
        }

        var serverEnd = path.IndexOf('/', 2);
        if (serverEnd <= 2) {
            return false;
        }

        var shareStart = serverEnd + 1;
        if (shareStart >= path.Length) {
            return false;
        }

        var shareEnd = path.IndexOf('/', shareStart);

        return shareEnd < 0 ? path.Length > shareStart : shareEnd > shareStart;
        #endif
    }

    #if !NET6_0_OR_GREATER
    private static bool IsAsciiLetter(char ch) => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    #endif

    /// <summary>
    /// Validates a caller-supplied single path segment against the same lexical invariants used by
    /// <see cref="Normalize" /> for a single segment: non-empty, no separators, not `.`/`..`, no
    /// platform-invalid segment characters, and on Windows no reserved device names or trailing
    /// `.` / space.
    /// </summary>
    /// <param name="segment">The single segment to validate.</param>
    /// <param name="segmentLabel">Human-readable label used in exception messages.</param>
    /// <exception cref="InvalidPathException">`segment` is not a valid single path segment.</exception>
    internal static void ValidSinglePathSegment(string segment, string segmentLabel)
    {
        if (segment.Length == 0) {
            throw new InvalidPathException(segment, $"{segmentLabel} is empty. Pass a non-empty single path segment.");
        }

        if (segment.IndexOfAny(DirectorySeparators) >= 0) {
            throw new InvalidPathException(
                segment,
                $"{segmentLabel} '{segment}' contains a directory separator. Use the / operator or Join() to compose multi-segment paths."
            );
        }

        if (segment is "." or "..") {
            throw new InvalidPathException(
                segment,
                $"{segmentLabel} '{segment}' is not a valid single path segment. Use a normal segment name instead."
            );
        }

        ThrowIfInvalidSegmentChar(segment.AsSpan(), segment);
        ValidPlatformSinglePathSegment(segment.AsSpan(), segment);
    }

    private static void ValidateNormalizedPath(string normalized, string root, string attempted)
    {
        var tail = normalized.Length > root.Length ? normalized[root.Length..] : string.Empty;
        if (tail.Length == 0) {
            return;
        }

        if (!HostOs.IsWindows) {
            InvalidSegmentChar(normalized, root, attempted);

            return;
        }

        string? deferredPlatformReason = null;
        var remaining = tail.AsSpan();

        while (remaining.Length > 0) {
            var separatorIndex = remaining.IndexOf('/');
            var segment = separatorIndex >= 0 ? remaining[..separatorIndex] : remaining;

            if (segment.Length > 0) {
                ThrowIfInvalidSegmentChar(segment, attempted);

                if (deferredPlatformReason is null
                 && TryGetPlatformSinglePathSegmentError(segment, out var reason)) {
                    deferredPlatformReason = reason;
                }
            }

            if (separatorIndex < 0) {
                break;
            }

            remaining = remaining[(separatorIndex + 1)..];
        }

        if (deferredPlatformReason is not null) {
            throw new InvalidPathException(attempted, deferredPlatformReason);
        }
    }

    private static void ValidPlatformSinglePathSegment(ReadOnlySpan<char> segment, string attempted)
    {
        if (TryGetPlatformSinglePathSegmentError(segment, out var reason)) {
            throw new InvalidPathException(attempted, reason);
        }
    }

    private static void InvalidSegmentChar(string normalized, string root, string attempted)
    {
        var tail = normalized.Length > root.Length ? normalized[root.Length..] : string.Empty;
        ThrowIfInvalidSegmentChar(tail.AsSpan(), attempted);
    }

    private static bool TryGetPlatformSinglePathSegmentError(
        ReadOnlySpan<char> segment,
        [NotNullWhen(true)] out string? reason
    )
    {
        reason = null;
        if (!HostOs.IsWindows || segment.Length == 0) {
            return false;
        }

        if (segment[^1] is '.' or ' ') {
            reason = $"Windows path segment '{segment.ToString()}' must not end with '.' or space.";

            return true;
        }

        var extensionIndex = segment.IndexOf('.');
        var stem = extensionIndex >= 0 ? segment[..extensionIndex] : segment;
        var isReservedStem = stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
                          || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                          || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                          || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                          || stem.Length == 4
                          && stem[3] is >= '1' and <= '9' or '\u00B9' or '\u00B2' or '\u00B3'
                          && (stem[..3].Equals("COM", StringComparison.OrdinalIgnoreCase)
                           || stem[..3].Equals("LPT", StringComparison.OrdinalIgnoreCase));

        if (!isReservedStem) {
            return false;
        }

        reason = $"Windows path segment '{segment.ToString()}' uses reserved device name '{stem.ToString()}'.";

        return true;
    }

    private static void ThrowIfInvalidSegmentChar(ReadOnlySpan<char> value, string attempted)
    {
        var badIndex = value.IndexOfAny(HostOs.InvalidSegmentChars);
        if (badIndex < 0) {
            return;
        }

        ThrowInvalidSegmentChar(attempted, value[badIndex]);
    }

    private static void ThrowInvalidSegmentChar(string attempted, char ch)
    {
        throw new InvalidPathException(
            attempted,
            $"Contains invalid path character '{(char.IsControl(ch) ? ' ' : ch)}' (0x{(int) ch:X4}). "
          + "Platform-specific invalid-char set is documented on HostOs.InvalidSegmentChars"
        );
    }
}
