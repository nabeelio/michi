using System;
using System.Text;

namespace Michi.Internal;

/// <summary>
/// Internal helper for tilde (<c>~</c>) and environment-variable expansion. Both
/// are optional — only invoked when the matching <see cref="MPathOptions"/> flag
/// is <c>true</c>. Errors use <see cref="InvalidPathException"/> with D-35b
/// message wording.
/// </summary>
internal static class TildeExpander {
    /// <summary>
    /// If <paramref name="path"/> starts with <c>~</c> or <c>~/</c> (forward slash
    /// only — backslash variants are normalized earlier), replaces the tilde with
    /// <see cref="Environment.GetFolderPath"/> for <see cref="Environment.SpecialFolder.UserProfile"/>.
    /// If UserProfile is empty (host has no user-profile concept), throws
    /// <see cref="InvalidPathException"/>.
    /// </summary>
    /// <param name="path">The input path. Non-null.</param>
    /// <returns>The path with any leading tilde expanded; unchanged otherwise.</returns>
    internal static string ExpandTilde(string path)
    {
        #if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(path);
        #else
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        #endif

        // Only expand if the path IS "~" exactly, or starts with "~/". Do NOT
        // expand "~user/foo" (home of another user) — that is a Unix-only shell
        // concept outside Phase 1 scope.
        if (path == "~") {
            return GetUserProfileOrThrow(path);
        }

        if (path.Length >= 2 && path[0] == '~' && (path[1] == '/' || path[1] == '\\')) {
            var home = GetUserProfileOrThrow(path);
            // Drop the leading '~'; keep the rest starting from index 1 which is
            // the separator, so the result is "{home}/remainder". Use AsSpan +
            // Concat on net6+ to satisfy CA1845 (avoid intermediate Substring
            // allocation); fall back to the allocating form on netstandard2.1
            // where the ReadOnlySpan<char> overload of string.Concat is absent.
            #if NET6_0_OR_GREATER
            return string.Concat(home.AsSpan(), path.AsSpan(1));
            #else
            return home + path.Substring(1);
            #endif
        }

        return path;
    }

    private static string GetUserProfileOrThrow(string attemptedPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) {
            throw new InvalidPathException(
                attemptedPath,
                "Tilde expansion requested but UserProfile folder is not available on this host"
            );
        }

        return home;
    }

    /// <summary>
    /// Expands environment-variable references in <paramref name="path"/>. On Windows,
    /// <c>%VAR%</c> is delegated to <see cref="Environment.ExpandEnvironmentVariables"/>
    /// (BCL semantics: undefined variables remain literal). On Unix, a small explicit
    /// parser handles <c>$VAR</c> and <c>${VAR}</c>; undefined variables throw
    /// <see cref="InvalidPathException"/>.
    /// </summary>
    /// <param name="path">The input path. Non-null.</param>
    /// <returns>The path with environment-variable references replaced.</returns>
    internal static string ExpandEnvironmentVariables(string path)
    {
        #if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(path);
        #else
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        #endif

        if (HostOs.IsWindows) {
            // BCL Environment.ExpandEnvironmentVariables handles %VAR% on Windows.
            // Undefined variables remain as the literal "%VAR%" per BCL contract —
            // we do not override this because Windows consumers expect BCL behavior.
            return Environment.ExpandEnvironmentVariables(path);
        }

        // Unix: parse $VAR and ${VAR}. A '$' followed by '{' requires a closing '}';
        // otherwise the variable name is the longest sequence of [A-Za-z_][A-Za-z0-9_]*.
        var sb = new StringBuilder(path.Length);
        int i = 0;
        while (i < path.Length) {
            char c = path[i];
            if (c != '$') {
                sb.Append(c);
                i++;

                continue;
            }

            // c == '$'
            if (i + 1 >= path.Length) {
                // trailing '$' — keep literal
                sb.Append(c);
                i++;

                continue;
            }

            char next = path[i + 1];
            string varName;
            int consumed; // total chars consumed INCLUDING the leading '$'
            if (next == '{') {
                int end = path.IndexOf('}', i + 2);
                if (end < 0) {
                    throw new InvalidPathException(
                        path,
                        "Unterminated '${' in environment-variable reference; add a closing '}' or escape the '$' with $$"
                    );
                }

                varName = path.Substring(i + 2, end - (i + 2));
                consumed = (end - i) + 1;
            } else if (IsVarNameStart(next)) {
                int end = i + 2;
                while (end < path.Length && IsVarNameContinuation(path[end]))
                    end++;

                varName = path.Substring(i + 1, end - (i + 1));
                consumed = end - i;
            } else {
                // '$' followed by something that can't start a var name — keep '$' literal
                sb.Append(c);
                i++;

                continue;
            }

            if (string.IsNullOrEmpty(varName)) {
                throw new InvalidPathException(
                    path,
                    "Empty environment-variable name in '$' reference"
                );
            }

            var value = Environment.GetEnvironmentVariable(varName);
            if (value is null) {
                throw new InvalidPathException(
                    path,
                    $"Environment variable '{varName}' referenced in path is not defined"
                );
            }

            sb.Append(value);
            i += consumed;
        }

        return sb.ToString();
    }

    private static bool IsVarNameStart(char c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

    private static bool IsVarNameContinuation(char c) =>
            IsVarNameStart(c) || (c >= '0' && c <= '9');
}
