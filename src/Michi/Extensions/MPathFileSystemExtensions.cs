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
                    Directory.Delete(entry, true);
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

#region Interop

    /// <summary>
    /// Returns a <see cref="FileInfo" /> pointing at this path. The file does not need
    /// to exist -- construction is I/O-free and matches `new FileInfo(path)` semantics.
    /// Use when the richer BCL API (timestamps, attributes, streams) is needed.
    /// </summary>
    /// <param name="path">The path to wrap. Must not be null.</param>
    /// <returns>A <see cref="FileInfo" /> whose <see cref="FileSystemInfo.FullName" /> is `path.Value`.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    public static FileInfo ToFileInfo(this MPath path)
    {
        Guard.NotNull(path);

        return new(path.Value);
    }

    /// <summary>
    /// Returns a <see cref="DirectoryInfo" /> pointing at this path. The directory does
    /// not need to exist -- construction is I/O-free and matches `new DirectoryInfo(path)`
    /// semantics. Use when the richer BCL API (timestamps, attributes, enumeration) is
    /// needed.
    /// </summary>
    /// <param name="path">The path to wrap. Must not be null.</param>
    /// <returns>A <see cref="DirectoryInfo" /> whose <see cref="FileSystemInfo.FullName" /> is `path.Value`.</returns>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    public static DirectoryInfo ToDirectoryInfo(this MPath path)
    {
        Guard.NotNull(path);

        return new(path.Value);
    }

#endregion

#region Enumeration

    /// <summary>
    /// Lazily enumerates files under this path that match `searchPattern`. Iteration is
    /// streaming -- the underlying filesystem is only walked as elements are consumed.
    /// </summary>
    /// <param name="path">The directory to enumerate. Must exist and must be a directory.</param>
    /// <param name="searchPattern">
    /// Glob-style pattern passed to <see cref="Directory.EnumerateFiles(string, string, SearchOption)" />.
    /// </param>
    /// <param name="searchOption">Top-level only or recursive.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}" /> of <see cref="MPath" /> yielded lazily; ordering is
    /// filesystem-dependent and not guaranteed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// `path` or `searchPattern` is null. Thrown eagerly at call time, not deferred to
    /// iteration.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// `path` does not exist. Thrown by the underlying BCL enumeration on first
    /// `MoveNext` -- consumers that never iterate will not see it.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// `searchPattern` is invalid or `path` points at a file, not a directory. Thrown by
    /// the underlying BCL enumeration on first `MoveNext`.
    /// </exception>
    /// <remarks>
    /// Materialize with `ToList()` / `ToArray()` when a snapshot is needed. Without
    /// materialization, two iterations of the same query walk the filesystem twice and
    /// may return different results if the tree changed in between.
    /// </remarks>
    public static IEnumerable<MPath> EnumerateFiles(
        this MPath path,
        string searchPattern,
        SearchOption searchOption
    )
    {
        // Eager checks run before the iterator is returned. If the `foreach` lived in this
        // method body directly (with `yield return`), argument validation would be deferred
        // until the caller called `MoveNext` -- silently letting null receivers pass if the
        // query was built but never iterated.
        Guard.NotNull(path);
        Guard.NotNull(searchPattern);

        return EnumerateFilesIterator(path, searchPattern, searchOption);
    }

    private static IEnumerable<MPath> EnumerateFilesIterator(
        MPath path,
        string searchPattern,
        SearchOption searchOption
    )
    {
        foreach (var raw in Directory.EnumerateFiles(path.Value, searchPattern, searchOption)) {
            yield return MPath.From(raw);
        }
    }

    /// <summary>
    /// Lazily enumerates directories under this path that match `searchPattern`. Iteration
    /// is streaming -- the underlying filesystem is only walked as elements are consumed.
    /// </summary>
    /// <param name="path">The directory to enumerate. Must exist and must be a directory.</param>
    /// <param name="searchPattern">
    /// Glob-style pattern passed to
    /// <see cref="Directory.EnumerateDirectories(string, string, SearchOption)" />.
    /// </param>
    /// <param name="searchOption">Top-level only or recursive.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}" /> of <see cref="MPath" /> yielded lazily; ordering is
    /// filesystem-dependent and not guaranteed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// `path` or `searchPattern` is null. Thrown eagerly at call time, not deferred to
    /// iteration.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// `path` does not exist. Thrown by the underlying BCL enumeration on first
    /// `MoveNext`.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// `searchPattern` is invalid or `path` points at a file, not a directory. Thrown by
    /// the underlying BCL enumeration on first `MoveNext`.
    /// </exception>
    public static IEnumerable<MPath> EnumerateDirectories(
        this MPath path,
        string searchPattern,
        SearchOption searchOption
    )
    {
        Guard.NotNull(path);
        Guard.NotNull(searchPattern);

        return EnumerateDirectoriesIterator(path, searchPattern, searchOption);
    }

    private static IEnumerable<MPath> EnumerateDirectoriesIterator(
        MPath path,
        string searchPattern,
        SearchOption searchOption
    )
    {
        foreach (var raw in Directory.EnumerateDirectories(
                     path.Value,
                     searchPattern,
                     searchOption
                 )) {
            yield return MPath.From(raw);
        }
    }

#endregion

#region Deletion

    /// <summary>
    /// Deletes the file at this path. Idempotent -- if the file does not exist, this is
    /// a no-op. Throws if a *directory* exists at the path rather than silently treating
    /// the wrong-kind target as success.
    /// </summary>
    /// <param name="path">The file path to delete. Must not be null.</param>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    /// <exception cref="IOException">
    /// A directory (not a file) exists at `path`. The message names the path and points
    /// at <see cref="DeleteDirectory" /> as the remedy.
    /// </exception>
    /// <remarks>
    /// The wrong-kind probe uses <see cref="Directory.Exists(string)" />, which follows
    /// directory symlinks. A hostile symlink pointing at a directory will therefore trip
    /// this guard -- intentionally, since the consumer asked to delete a *file* and a
    /// traversable directory is not that.
    /// </remarks>
    public static void DeleteFile(this MPath path)
    {
        Guard.NotNull(path);

        if (Directory.Exists(path.Value)) {
            throw new IOException(
                $"Cannot DeleteFile '{path.Value}': a directory exists at this path. "
              + "DeleteFile will not delete directories -- use DeleteDirectory() if you "
              + "intended to delete the directory tree."
            );
        }

        // File.Delete is already idempotent when the file is missing -- it just returns.
        File.Delete(path.Value);
    }

    /// <summary>
    /// Deletes the directory at this path. Idempotent -- if the directory does not
    /// exist, this is a no-op. Recursive by default; pass `recursive: false` to require
    /// an empty directory (matching <see cref="Directory.Delete(string, bool)" />
    /// semantics: a non-empty directory throws). Throws if a *file* exists at the path
    /// rather than silently treating the wrong-kind target as success.
    /// </summary>
    /// <param name="path">The directory path to delete. Must not be null.</param>
    /// <param name="recursive">
    /// When `true` (default), removes the directory and all contents. When `false`,
    /// throws <see cref="IOException" /> if the directory is non-empty.
    /// </param>
    /// <exception cref="ArgumentNullException">`path` is null.</exception>
    /// <exception cref="IOException">
    /// A file (not a directory) exists at `path` -- the message names the path and
    /// points at <see cref="DeleteFile" /> as the remedy -- or `recursive` is `false`
    /// and the directory is non-empty (BCL message).
    /// </exception>
    /// <remarks>
    /// The wrong-kind probe runs BEFORE the idempotency probe. Otherwise a path where a
    /// file exists would fall through to the "directory does not exist" no-op branch
    /// and silently succeed, hiding the wrong-kind mistake from the caller.
    /// <para>
    /// With `recursive: true` the underlying
    /// <see cref="Directory.Delete(string, bool)" /> follows directory symlinks. On an
    /// attacker-controlled filesystem a hostile symlink can cause deletion of data
    /// outside the target. See the README "Security" section.
    /// </para>
    /// </remarks>
    public static void DeleteDirectory(this MPath path, bool recursive = true)
    {
        Guard.NotNull(path);

        // Wrong-kind check runs BEFORE the missing-directory idempotency check so a file
        // at the path produces the explicit D-13 error instead of silently returning.
        if (File.Exists(path.Value)) {
            throw new IOException(
                $"Cannot DeleteDirectory '{path.Value}': a file exists at this path. "
              + "DeleteDirectory will not delete files -- use DeleteFile() if you "
              + "intended to delete the file."
            );
        }

        if (!Directory.Exists(path.Value)) {
            return;
        }

        Directory.Delete(path.Value, recursive);
    }

#endregion
}
