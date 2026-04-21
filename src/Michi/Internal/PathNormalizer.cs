using Michi.Exceptions;

namespace Michi.Internal;

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
    /// <summary>Normalizes <paramref name="path" /> into a canonical absolute form.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path" /> or <paramref name="options" /> is null.</exception>
    /// <exception cref="InvalidPathException">
    /// Path is empty, can't resolve to absolute, or contains invalid characters.
    /// </exception>
    internal static NormalizationResult Normalize(string path, MPathOptions options, string? relativeTo = null)
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

        // Path.GetFullPath(path, basePath) ignores basePath when path is rooted, but still requires
        // basePath to be rooted. For rooted inputs we pass AppContext.BaseDirectory (always rooted)
        // and let the BCL ignore it. For relative inputs, the base must be real.
        string basePath;
        if (Path.IsPathRooted(path)) {
            basePath = AppContext.BaseDirectory;
        } else {
            basePath = relativeTo ?? options.BaseDirectory;
            if (string.IsNullOrWhiteSpace(basePath) || !Path.IsPathRooted(basePath)) {
                throw new InvalidPathException(
                    attempted,
                    "Not a valid absolute path. Provide an absolute path, set MPathOptions.BaseDirectory, or pass a relativeTo argument"
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

        // Cross-platform invalid-char guard. Linux's GetInvalidPathChars only returns '\0' so this is
        // mostly a Windows safety net, but running it uniformly keeps error surfaces consistent.
        var invalidPathChars = Path.GetInvalidPathChars();
        var badIndex = normalized.IndexOfAny(invalidPathChars);
        if (badIndex >= 0) {
            var ch = normalized[badIndex];

            throw new InvalidPathException(
                attempted,
                $"Contains invalid path character '{ch}' (0x{(int) ch:X4}). See Path.GetInvalidPathChars() for the current platform"
            );
        }

        // Segment-level invalid-char guard on Windows. GetInvalidPathChars() on Windows returns only
        // control chars 0x00-0x1F; the reserved-in-filenames set (< > | " * ?) is in
        // GetInvalidFileNameChars(). We check per segment (skipping the root) so that ':' at the
        // drive-letter root is still allowed while ':' embedded in a filename is rejected. See
        // PITFALLS C-10.
        if (HostOs.IsWindows) {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var tail = normalized.Length > root.Length ? normalized[root.Length..] : string.Empty;
            var segments = tail.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments) {
                var segBadIndex = segment.IndexOfAny(invalidFileNameChars);
                if (segBadIndex < 0) {
                    continue;
                }

                var ch = segment[segBadIndex];

                throw new InvalidPathException(
                    attempted,
                    $"Contains invalid path character '{ch}' (0x{(int) ch:X4}). See Path.GetInvalidFileNameChars() for the current platform"
                );
            }
        }

        return new(normalized, root);
    }
}
