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
#region Existence

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

#endregion

#region Creation

    /// <summary>
    /// Creates the directory at this path, along with any missing intermediate parents.
    /// Idempotent -- calling on an already-existing directory is a no-op. Returns the same
    /// <see cref="MPath" /> instance for fluent chaining.
    /// </summary>
    /// <param name="path">The directory path to create. Must not be null.</param>
    /// <returns>The same `path` instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    /// <exception cref="IOException">
    /// A file (not a directory) already exists at `path`. This is the BCL-native exception
    /// from <see cref="Directory.CreateDirectory(string)" />; it is not wrapped.
    /// </exception>
    /// <example>
    ///     <code>
    /// var logs = MPath.From("/var/app/logs").CreateDirectory();
    /// logs.DirectoryExists().ShouldBeTrue();
    ///     </code>
    /// </example>
    public static MPath CreateDirectory(this MPath path)
    {
        Guard.NotNull(path);

        Directory.CreateDirectory(path.Value);

        return path;
    }

    /// <summary>
    /// Creates the parent directory of this path if it does not already exist. Does NOT
    /// create the path itself. Returns the same <see cref="MPath" /> for fluent chaining.
    /// No-op when `path` is a root (has no parent).
    /// </summary>
    /// <param name="path">The path whose parent chain should be ensured. Must not be null.</param>
    /// <returns>The same `path` instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    /// <remarks>
    /// A later file-create race is possible -- another process can delete the parent between
    /// this call and a subsequent create operation. For security-critical code paths, prefer
    /// atomic create-if-not-exists APIs (for example, `FileStream` with `FileMode.CreateNew`).
    /// </remarks>
    public static MPath EnsureParentExists(this MPath path)
    {
        Guard.NotNull(path);

        if (path.TryGetParent(out var parent) && parent is not null) {
            Directory.CreateDirectory(parent.Value);
        }

        return path;
    }

    /// <summary>
    /// Ensures the directory at this path exists and is empty. If it already exists, every
    /// child (files and subdirectories, recursively) is removed *in place*; the root
    /// directory itself is not deleted. If a file already exists at the path, throws rather
    /// than replacing the file. Returns the same <see cref="MPath" /> for fluent chaining.
    /// </summary>
    /// <param name="path">The directory path to create or clear. Must not be null.</param>
    /// <returns>The same `path` instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    /// <exception cref="IOException">
    /// A file (not a directory) exists at `path`. The file is NOT replaced; resolve the
    /// conflict manually before retrying.
    /// </exception>
    /// <remarks>
    /// The clear uses <see cref="Directory.Delete(string, bool)" /> on each child
    /// subdirectory, which follows directory symlinks by default. On a trusted filesystem
    /// this is correct; on an attacker-controlled filesystem a hostile symlink can cause
    /// deletion of data outside the target. See the README "Security" section.
    /// <para>
    /// Cost is O(n) in the tree size -- one enumeration plus one delete per entry. Consumers
    /// responsible for calling this on bounded inputs.
    /// </para>
    /// </remarks>
    public static MPath CreateOrClearDirectory(this MPath path)
    {
        Guard.NotNull(path);

        // Fail loudly on file-vs-directory mismatch rather than silently replacing the file.
        if (File.Exists(path.Value)) {
            throw new IOException(
                $"Cannot create-or-clear directory '{path.Value}': a file already exists at this path. "
                + "CreateOrClearDirectory will not replace a file with a directory. "
                + "Delete the file explicitly and retry."
            );
        }

        if (Directory.Exists(path.Value)) {
            // Clear contents in place, recursively, but leave the root directory itself.
            // Callers relying on a stable inode/handle for the root (for example, a watcher)
            // keep their invariant across the clear.
            foreach (var entry in Directory.EnumerateFileSystemEntries(path.Value)) {
                if (Directory.Exists(entry)) {
                    Directory.Delete(entry, recursive: true);
                } else {
                    File.Delete(entry);
                }
            }
        } else {
            Directory.CreateDirectory(path.Value);
        }

        return path;
    }

#endregion
}
