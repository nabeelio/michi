using Michi.Internal;

namespace Michi.Extensions;

/// <summary>
/// Opt-in filesystem extension methods on <see cref="MPath" />. Import with
/// `using Michi.Extensions;` when filesystem-facing operations are needed. The core `MPath`
/// type intentionally has no filesystem behavior.
/// </summary>
/// <remarks>
/// These methods delegate to <see cref="System.IO.File" /> and <see cref="System.IO.Directory" />
/// using <see cref="MPath.Value" /> (the OS-native form). No additional normalization is
/// performed -- `MPath.From` already normalized the path at construction time.
/// <para>
/// Symlinks are followed by the underlying BCL calls. If your threat model includes
/// attacker-controlled symlinks, canonicalize with `FileInfo.ResolveLinkTarget` or
/// `DirectoryInfo.ResolveLinkTarget` (net6.0+) before calling these methods. See the
/// README "Security" section for the full guarantees and non-guarantees.
/// </para>
/// </remarks>
public static class MPathFileSystemExtensions {
    /// <summary>
    /// Returns `true` only when a file exists at the path. A directory at the same path
    /// returns `false` (mirrors <see cref="File.Exists(string)" />).
    /// </summary>
    /// <param name="path">The path to probe. Must not be null.</param>
    /// <returns>`true` when a file exists at the path; `false` otherwise.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    public static bool FileExists(this MPath path)
    {
        Guard.NotNull(path);

        return File.Exists(path.Value);
    }

    /// <summary>
    /// Returns `true` only when a directory exists at the path. A file at the same path
    /// returns `false` (mirrors <see cref="Directory.Exists(string)" />).
    /// </summary>
    /// <param name="path">The path to probe. Must not be null.</param>
    /// <returns>`true` when a directory exists at the path; `false` otherwise.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    public static bool DirectoryExists(this MPath path)
    {
        Guard.NotNull(path);

        return Directory.Exists(path.Value);
    }
}
