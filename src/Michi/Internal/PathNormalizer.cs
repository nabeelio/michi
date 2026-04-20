using System.Text;

namespace Michi.Internal;

/// <summary>
/// Returned by <see cref="PathNormalizer.Normalize" />. Carries the canonical
/// forward-slash absolute path and the extracted root prefix.
/// </summary>
internal readonly struct NormalizationResult {
    /// <summary>
    /// Normalized absolute path. Always uses forward-slash. Never has a trailing
    /// slash unless the path IS the root (e.g., "/" on Unix, "C:/" on Windows).
    /// </summary>
    public string Normalized { get; }

    /// <summary>
    /// Root prefix in forward-slash form. "/" on Unix absolute paths, "C:/" (or
    /// similar drive letter) on Windows drive-letter paths, "//server/share" on
    /// UNC paths.
    /// </summary>
    public string Root { get; }

    public NormalizationResult(string normalized, string root)
    {
        Normalized = normalized;
        Root = root;
    }
}

/// <summary>
/// The six-step normalization pipeline per NORM-01 / CONTEXT D-11. Pure string
/// transformation — never touches the filesystem (no calls to the single-argument
/// <see cref="Path.GetFullPath(string)" /> overload; see PITFALLS C-04).
/// </summary>
internal static class PathNormalizer {
    /// <summary>
    /// Normalizes <paramref name="path" /> into a canonical absolute form.
    /// </summary>
    /// <param name="path">
    /// The raw input path. Null and empty/whitespace are rejected.
    /// </param>
    /// <param name="options">
    /// Options controlling tilde + env-var expansion and the base directory for relative-path resolution.
    /// </param>
    /// <param name="relativeTo">
    /// If non-null, used as the base for relative-path resolution INSTEAD of <see cref="MPathOptions.BaseDirectory" />.
    /// </param>
    /// <returns>
    /// A <see cref="NormalizationResult" /> with the canonical forward-slash form and the extracted root.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="path" /> is
    /// <c>
    /// null
    /// </c>
    /// .
    /// </exception>
    /// <exception cref="InvalidPathException">
    /// Thrown when the path cannot be normalized to a valid absolute path.
    /// </exception>
    internal static NormalizationResult Normalize(string path, MPathOptions options, string? relativeTo = null)
    {
        #if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);
        #else
        if (path is null) {
            throw new ArgumentNullException(
                nameof(path),
                "Path cannot be null. Use MPath.TryFrom to accept null input without exceptions."
            );
        }

        if (options is null) {
            throw new ArgumentNullException(
                nameof(options),
                "Options cannot be null. Pass MPathOptions.Default or construct a new MPathOptions via the record's 'with' expression."
            );
        }
        #endif

        var attempted = path; // preserve the original for error messages

        // Step 1: null/empty/whitespace rejection
        if (string.IsNullOrWhiteSpace(path)) {
            throw new InvalidPathException(
                attempted,
                "Path is empty. Provide a non-empty absolute or resolvable path string"
            );
        }

        // Step 2: environment-variable expansion (optional)
        if (options.ExpandEnvironmentVariables) {
            path = TildeExpander.ExpandEnvironmentVariables(path);
        }

        // Step 3: tilde expansion (optional)
        if (options.ExpandTilde) {
            path = TildeExpander.ExpandTilde(path);
        }

        // Step 4: relative-path resolution against base.
        // Detect absoluteness BEFORE separator normalization using the BCL's
        // per-platform rules on the original (separator-unmodified) string.
        if (!Path.IsPathRooted(path)) {
            var basePath = relativeTo ?? options.BaseDirectory;
            if (string.IsNullOrWhiteSpace(basePath) || !Path.IsPathRooted(basePath)) {
                throw new InvalidPathException(
                    attempted,
                    "Not a valid absolute path. Provide an absolute path, set MPathOptions.BaseDirectory, or pass a relativeTo argument"
                );
            }

            // Use the TWO-ARG overload only — single-arg touches the filesystem (PITFALLS C-04).
            try {
                path = Path.GetFullPath(path, basePath);
            } catch (Exception ex) when (ex is ArgumentException
                                      || ex is NotSupportedException
                                      || ex is PathTooLongException) {
                throw new InvalidPathException(
                    attempted,
                    $"Cannot resolve against base '{basePath}': {ex.Message}",
                    ex
                );
            }
        }

        // Step 5: extract root BEFORE separator normalization so UNC prefixes
        // survive the repeated-separator collapse (PITFALLS C-06).
        var rootRaw = Path.GetPathRoot(path) ?? string.Empty;
        var tail = path.Substring(rootRaw.Length);

        // Step 6: normalize separators in both root and tail
        var rootFs = rootRaw.Replace('\\', '/');
        var tailFs = tail.Replace('\\', '/');

        // Step 7: resolve . and .. in tail via pure-string walk (PITFALLS C-04).
        // Also collapses repeated separators in the tail (but NOT in the root —
        // UNC root "//server/share" has a structural leading //).
        //
        // `isAbsoluteContext` tells the walker that the tail belongs to a rooted
        // path — so any `..` that would escape the root (stack empty) is DROPPED,
        // matching filesystem convention (`/..` -> `/`). This is the fix for the
        // previously-missed case where the root was stripped off the tail BEFORE
        // ResolveDotSegments was called: without this flag, an absolute path like
        // `/foo/../../bar` would produce `"../bar"` because the walker could not
        // tell that the tail originated from a rooted path.
        var isAbsoluteContext = rootFs.Length > 0;
        var tailResolved = ResolveDotSegments(tailFs, isAbsoluteContext);

        // Step 8: reassemble. If the walker returned a "/"-prefixed result because
        // the tail had a literal leading slash, strip it to avoid a double-slash
        // when concatenating with rootFs (which ends in '/' for drive/UNC roots).
        string combined;
        if (tailResolved.Length > 0
         && tailResolved[0] == '/'
         && rootFs.Length > 0
         && rootFs[rootFs.Length - 1] == '/') {
            #if NET6_0_OR_GREATER
            combined = string.Concat(rootFs.AsSpan(), tailResolved.AsSpan(1));
            #else
            combined = rootFs + tailResolved.Substring(1);
            #endif
        } else {
            combined = rootFs + tailResolved;
        }

        // Step 9: strip trailing slash unless combined IS the root
        string normalized;
        if (combined.Length > rootFs.Length && combined[combined.Length - 1] == '/') {
            normalized = combined.TrimEnd('/');
            // If the trim ate too much (shouldn't happen given the length guard), restore
            if (normalized.Length < rootFs.Length)
                normalized = combined;
        } else {
            normalized = combined;
        }

        // Step 10: validate the result is absolute
        if (!Path.IsPathRooted(normalized)) {
            throw new InvalidPathException(
                attempted,
                "Not a valid absolute path. Provide an absolute path, set MPathOptions.BaseDirectory, or pass a relativeTo argument"
            );
        }

        // Step 11: validate invalid chars per-platform (C-10). Path.GetInvalidPathChars
        // returns only '\0' on Linux — the platform is the authority, we do not
        // union sets.
        var invalidChars = Path.GetInvalidPathChars();
        foreach (var ch in normalized) {
            for (var k = 0; k < invalidChars.Length; k++) {
                if (ch == invalidChars[k]) {
                    throw new InvalidPathException(
                        attempted,
                        $"Contains invalid path character '{ch}' (0x{(int) ch:X4}). See Path.GetInvalidPathChars() for the current platform"
                    );
                }
            }
        }

        // Step 12: UNC on non-Windows is not supported. A raw "\\server\share" input
        // is not rooted on Unix per Path.IsPathRooted, so the relative-resolution
        // branch above will already have thrown with "Not a valid absolute path".
        // No additional guard needed here.

        return new(normalized, rootFs);
    }

    /// <summary>
    /// Walks a forward-slash tail and resolves
    /// <c>
    /// .
    /// </c>
    /// /
    /// <c>
    /// ..
    /// </c>
    /// segments mathematically.
    /// Also collapses repeated
    /// <c>
    /// /
    /// </c>
    /// within the tail. Does NOT touch the root. A
    /// leading
    /// <c>
    /// /
    /// </c>
    /// in the tail is preserved as-is.
    /// </summary>
    /// <param name="tail">
    /// The tail segment to resolve.
    /// </param>
    /// <param name="isAbsoluteContext">
    /// When
    /// <c>
    /// true
    /// </c>
    /// , the tail is known to belong to an already-rooted path
    /// (e.g., the caller has already peeled off a root like
    /// <c>
    /// /
    /// </c>
    /// or
    /// <c>
    /// C:/
    /// </c>
    /// and passed only the segments beneath). Any
    /// <c>
    /// ..
    /// </c>
    /// that would escape the
    /// stack in this context is DROPPED (filesystem convention:
    /// <c>
    /// /..
    /// </c>
    /// ->
    /// <c>
    /// /
    /// </c>
    /// ).
    /// When
    /// <c>
    /// false
    /// </c>
    /// , the tail is genuinely relative and
    /// <c>
    /// ..
    /// </c>
    /// past the
    /// stack boundary is kept in the output so the caller's absolute-validation
    /// step can reject the path.
    /// </param>
    private static string ResolveDotSegments(string tail, bool isAbsoluteContext)
    {
        if (string.IsNullOrEmpty(tail))
            return tail;

        // `leadingSlash` = the output should be prefixed with '/'.
        // `dropEscapingDotDot` = `..` that would escape the stack is DROPPED
        //                       (filesystem convention for absolute paths).
        var leadingSlash = tail[0] == '/';
        var dropEscapingDotDot = leadingSlash || isAbsoluteContext;
        // Split on '/', filter empties (which collapses repeated '/'), then walk.
        var raw = tail.Split('/');
        var stack = new List<string>(raw.Length);
        foreach (var seg in raw) {
            if (seg.Length == 0) {
                // empty segment from repeated separator — collapse
                continue;
            }

            if (seg == ".") {
                continue;
            }

            if (seg == "..") {
                if (stack.Count > 0 && stack[stack.Count - 1] != "..") {
                    stack.RemoveAt(stack.Count - 1);
                } else if (!dropEscapingDotDot) {
                    // relative tail (no absolute context) — keep ".." in the output
                    // because it represents going above the base; the caller's
                    // absolute-validation step will reject if this escapes the root.
                    stack.Add("..");
                }

                // in absolute context and stack is empty: "/..", which resolves to "/" —
                // just ignore the ".." (filesystem convention)
                continue;
            }

            stack.Add(seg);
        }

        if (stack.Count == 0) {
            return leadingSlash ? "/" : string.Empty;
        }

        var sb = new StringBuilder();
        if (leadingSlash)
            sb.Append('/');

        for (var i = 0; i < stack.Count; i++) {
            if (i > 0)
                sb.Append('/');

            sb.Append(stack[i]);
        }

        return sb.ToString();
    }
}
