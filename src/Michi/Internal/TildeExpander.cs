using System.Text;
using Michi.Exceptions;

namespace Michi.Internal;

/// <summary>
/// Tilde (`~`) and env-var expansion. Only runs when the matching <see cref="MPathOptions" />
/// flag is `true`. Errors surface as <see cref="InvalidPathException" />.
/// </summary>
internal static class TildeExpander {
    /// <summary>
    /// If <paramref name="path" /> is `~` or starts with `~/` (or `~\`), replaces the
    /// tilde with <see cref="Environment.SpecialFolder.UserProfile" />. Throws
    /// <see cref="InvalidPathException" /> if the host has no user-profile folder.
    /// </summary>
    internal static string ExpandTilde(string path)
    {
        Guard.NotNull(path);

        // Only expand bare "~" or "~/" prefix. "~user/foo" (another user's home) is a Unix shell
        // concept and isn't supported.
        if (path == "~") {
            return GetUserProfileOrThrow(path);
        }

        if (path.Length >= 2 && path[0] is '~' && path[1] is '/' or '\\') {
            var home = GetUserProfileOrThrow(path);
            // Keep the separator so the result is "{home}/remainder". AsSpan + Concat avoids
            // CA1845 on net6+; netstandard2.1 lacks the span overload so falls back to the allocating form.
            #if NET6_0_OR_GREATER
            return string.Concat(home.AsSpan(), path.AsSpan(1));
            #else
            return home + path[1..];
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
    /// Expands env-var references. Windows delegates `%VAR%` to
    /// <see cref="Environment.ExpandEnvironmentVariables" /> (undefined vars stay literal per BCL contract).
    /// Unix parses `$VAR` and `${VAR}`; undefined vars throw <see cref="InvalidPathException" />.
    /// </summary>
    internal static string ExpandEnvironmentVariables(string path)
    {
        Guard.NotNull(path);

        if (HostOs.IsWindows) {
            // BCL contract: undefined "%VAR%" stays literal. Don't override -- Windows consumers expect it.
            return Environment.ExpandEnvironmentVariables(path);
        }

        // Fast path: no '$' anywhere means no expansion work to do. Most paths hit this.
        var firstDollar = path.IndexOf('$');
        if (firstDollar < 0) {
            return path;
        }

        // Unix: $VAR and ${VAR}. ${...} must close; otherwise the var name is [A-Za-z_][A-Za-z0-9_]*.
        // We copy literal runs in one Append call per run instead of one char at a time.
        var sb = new StringBuilder(path.Length);
        sb.Append(path, 0, firstDollar);
        var i = firstDollar;
        var runStart = i;

        while (i < path.Length) {
            if (path[i] != '$') {
                i++;

                continue;
            }

            // Flush the literal run-up to this '$' before handling it.
            if (i > runStart) {
                sb.Append(path, runStart, i - runStart);
            }

            // trailing '$' -- keep literal
            if (i + 1 >= path.Length) {
                sb.Append('$');
                i++;
                runStart = i;

                continue;
            }

            var next = path[i + 1];
            string varName;
            int consumed; // chars consumed including the leading '$'
            if (next == '{') {
                var end = path.IndexOf('}', i + 2);
                if (end < 0) {
                    throw new InvalidPathException(
                        path,
                        "Unterminated '${' in environment-variable reference; add a closing '}' or escape the '$' with $$"
                    );
                }

                varName = path[(i + 2)..end];
                consumed = end - i + 1;
            } else if (IsVarNameStart(next)) {
                var end = i + 2;
                while (end < path.Length && IsVarNameContinuation(path[end]))
                    end++;

                varName = path[(i + 1)..end];
                consumed = end - i;
            } else {
                // '$' followed by something invalid for a var name -- keep '$' literal
                sb.Append('$');
                i++;
                runStart = i;

                continue;
            }

            if (string.IsNullOrEmpty(varName)) {
                throw new InvalidPathException(path, "Empty environment-variable name in '$' reference");
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
            runStart = i;
        }

        // Flush any trailing literal run.
        if (runStart < path.Length) {
            sb.Append(path, runStart, path.Length - runStart);
        }

        return sb.ToString();
    }

    private static bool IsVarNameStart(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';

    private static bool IsVarNameContinuation(char c) => IsVarNameStart(c) || c is >= '0' and <= '9';
}
