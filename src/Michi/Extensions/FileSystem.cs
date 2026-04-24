using Michi.Internal;

// File lives under src/Michi/Extensions/ (historical layout). Namespace is `Michi.FileSystem`
// to follow the LINQ / NodaTime convention of feature-domain naming rather than
// `{Lib}.Extensions.{Concern}`. The folder name is retained for git-history continuity;
// the namespace is the actual public-API contract.
// ReSharper disable once CheckNamespace
namespace Michi.FileSystem;

/// <summary>
/// Opt-in filesystem extension methods on <see cref="MPath" />. Import with
/// `using Michi.FileSystem;` when filesystem-facing operations are needed. The core `MPath`
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
public static class MPathExtensions {
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

#region Move and copy

    /// <summary>
    /// Moves the file or directory at this path to `destination`. Destination resolution
    /// follows cp/mv semantics: if `destination` already exists and is a directory, it is
    /// treated as a parent container and the actual target becomes
    /// `destination / source.Name`. Otherwise `destination` is the exact target. The
    /// parent of the resolved target is auto-created when missing.
    /// </summary>
    /// <param name="source">The file or directory to move. Must not be null.</param>
    /// <param name="destination">The target path or container directory. Must not be null.</param>
    /// <param name="policy">
    /// Conflict policy. Default <see cref="ExistsPolicy.Fail" /> -- any existing file or
    /// directory at the resolved target throws. Use the named combinations (for example
    /// <see cref="ExistsPolicy.MergeAndOverwrite" />) for non-default behavior. Conflicting
    /// flag combinations throw <see cref="ArgumentException" /> before any filesystem I/O.
    /// </param>
    /// <exception cref="ArgumentNullException">`source` or `destination` is null.</exception>
    /// <exception cref="ArgumentException">
    /// `policy` combines multiple file behaviors, multiple directory behaviors, or contains
    /// unknown bits. Thrown before any filesystem state is probed.
    /// </exception>
    /// <exception cref="IOException">
    /// Wrong-kind conflict (directory source onto an existing file at the resolved target),
    /// a fail-on-collision result, or an underlying BCL I/O error.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// `source` points at a missing file. Surfaced from the BCL `File.Move` call so consumers
    /// get the standard exception shape.
    /// </exception>
    /// <remarks>
    /// `FileSkip` on Move means "do not move the file" -- both the source and destination are
    /// left in place when a skip occurs. Consumers wanting "move only when destination is
    /// free" should use the default <see cref="ExistsPolicy.Fail" /> and react to the
    /// <see cref="IOException" />.
    /// <para>
    /// `FileOverwrite` on netstandard2.1 is implemented as Delete + Move (the three-argument
    /// `File.Move(src, dst, overwrite)` overload only exists on net5.0+). The window between
    /// the delete and the move is non-atomic on netstandard2.1; on net8/net10 we still use
    /// the same sequence so behavior is uniform across TFMs.
    /// </para>
    /// <para>
    /// `FileOverwriteIfNewer` compares <see cref="File.GetLastWriteTimeUtc(string)" /> in UTC
    /// to avoid DST-related off-by-one-hour comparisons.
    /// </para>
    /// <para>
    /// `DirectoryReplace` deletes the resolved destination tree before moving. The delete
    /// uses <see cref="Directory.Delete(string, bool)" /> which follows directory symlinks;
    /// see the README "Security" section for the threat-model implications.
    /// </para>
    /// <para>
    /// The destination probe (`Directory.Exists` / `File.Exists`) and the subsequent
    /// move/copy are not atomic with respect to other processes (TOCTOU). On adversarial
    /// filesystems use higher-level locking or atomic file APIs.
    /// </para>
    /// </remarks>
    public static void MoveTo(
        this MPath source,
        MPath destination,
        ExistsPolicy policy = ExistsPolicy.Fail
    )
    {
        Guard.NotNull(source);
        Guard.NotNull(destination);
        ExistsPolicyValidator.Validate(policy, nameof(policy));

        var resolvedDest = ResolveDestination(source, destination);

        if (PathsEqual(source, resolvedDest)) {
            // Same path -- treat as a successful no-op rather than dispatching to the BCL,
            // which has surprising same-path behavior (File.Move throws; Directory.Move
            // succeeds with no-op only sometimes).
            return;
        }

        if (Directory.Exists(source.Value)) {
            MoveDirectoryWithPolicy(source, resolvedDest, policy);
        } else if (File.Exists(source.Value)) {
            MoveFileWithPolicy(source, resolvedDest, policy);
        } else {
            // Source missing. Defer to the BCL so consumers get the standard
            // FileNotFoundException shape and message.
            File.Move(source.Value, resolvedDest.Value);
        }
    }

    /// <summary>
    /// Copies the file or directory at this path to `destination`. Behaves like
    /// <see cref="MoveTo(MPath, MPath, ExistsPolicy)" /> with two differences: the source
    /// is preserved, and recursive directory copies accept an optional `filter` callback
    /// for include/skip decisions on each enumerated entry.
    /// </summary>
    /// <param name="source">The file or directory to copy. Must not be null.</param>
    /// <param name="destination">The target path or container directory. Must not be null.</param>
    /// <param name="policy">
    /// Conflict policy. Default <see cref="ExistsPolicy.Fail" /> -- any existing file or
    /// directory at the resolved target throws. See <see cref="MoveTo(MPath, MPath, ExistsPolicy)" />
    /// for the full per-bit semantics; behavior is identical except the source is left
    /// in place after the copy.
    /// </param>
    /// <param name="filter">
    /// Optional predicate invoked once per entry encountered during recursive directory
    /// copy. The argument is the SOURCE-side <see cref="MPath" /> of the entry (file or
    /// subdirectory). Returning `true` includes the entry; returning `false` skips it
    /// entirely -- a skipped subdirectory is not traversed. The filter is NOT invoked for
    /// flat file-to-file copies. The filter cannot rename or remap destinations -- it is
    /// include/skip only.
    /// </param>
    /// <exception cref="ArgumentNullException">`source` or `destination` is null.</exception>
    /// <exception cref="ArgumentException">
    /// `policy` combines multiple file behaviors, multiple directory behaviors, or contains
    /// unknown bits. Thrown before any filesystem state is probed.
    /// </exception>
    /// <exception cref="IOException">
    /// Wrong-kind conflict, fail-on-collision, or an underlying BCL I/O error.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// `source` points at a missing file. Surfaced from the BCL `File.Copy` call.
    /// </exception>
    /// <remarks>
    /// The filter is caller-supplied and runs synchronously on the calling thread. It must
    /// be a pure predicate over the source <see cref="MPath" />; exceptions thrown by the
    /// filter propagate to the caller and abort the in-progress copy. Filesystem mutations
    /// performed by the filter are the caller's responsibility.
    /// <para>
    /// Same TOCTOU caveat as <see cref="MoveTo(MPath, MPath, ExistsPolicy)" />: the
    /// destination probe and subsequent copy are not atomic with respect to other
    /// processes.
    /// </para>
    /// </remarks>
    public static void CopyTo(
        this MPath source,
        MPath destination,
        ExistsPolicy policy = ExistsPolicy.Fail,
        Func<MPath, bool>? filter = null
    )
    {
        Guard.NotNull(source);
        Guard.NotNull(destination);
        ExistsPolicyValidator.Validate(policy, nameof(policy));

        var resolvedDest = ResolveDestination(source, destination);

        if (PathsEqual(source, resolvedDest)) {
            return;
        }

        if (Directory.Exists(source.Value)) {
            CopyDirectoryWithPolicy(source, resolvedDest, policy, filter);
        } else if (File.Exists(source.Value)) {
            // Filter is intentionally ignored for flat file-to-file copies (D-25): the
            // filter applies only to recursive directory operations.
            CopyFileWithPolicy(source, resolvedDest, policy);
        } else {
            // Source missing -- defer to the BCL for the standard exception shape.
            File.Copy(source.Value, resolvedDest.Value);
        }
    }

#endregion

#region Move/copy helpers (shared)

    // cp/mv destination resolution (D-17 + D-18). When `destination` already exists as a
    // directory, the resolved target is `destination / source.Name`. Otherwise the
    // destination path is used exactly.
    private static MPath ResolveDestination(MPath source, MPath destination)
    {
        if (Directory.Exists(destination.Value)) {
            return destination / source.Name;
        }

        return destination;
    }

    // Host-OS-correct equality. MPath.Equals already routes through HostOs.PathComparer,
    // so this is a one-liner -- but pulling it out documents the intent and prevents
    // future contributors from comparing raw `Value` strings with the wrong comparison.
    private static bool PathsEqual(MPath a, MPath b) => a.Equals(b);

    private static void MoveFileWithPolicy(MPath source, MPath dest, ExistsPolicy policy)
    {
        dest.EnsureParentExists();

        if (File.Exists(dest.Value)) {
            if ((policy & ExistsPolicy.FileSkip) != 0) {
                // Skip: source stays in place, destination untouched.
                return;
            }

            if ((policy & ExistsPolicy.FileOverwrite) != 0) {
                File.Delete(dest.Value);
                File.Move(source.Value, dest.Value);

                return;
            }

            if ((policy & ExistsPolicy.FileOverwriteIfNewer) != 0) {
                if (File.GetLastWriteTimeUtc(source.Value) > File.GetLastWriteTimeUtc(dest.Value)) {
                    File.Delete(dest.Value);
                    File.Move(source.Value, dest.Value);
                }

                // Otherwise skip silently -- source older or equal, destination wins.
                return;
            }

            // Fail (default).
            throw new IOException(
                $"Cannot move file '{source.Value}' to '{dest.Value}': destination file already exists. "
              + "Pass ExistsPolicy.FileOverwrite, ExistsPolicy.FileOverwriteIfNewer, or ExistsPolicy.FileSkip to override."
            );
        }

        if (Directory.Exists(dest.Value)) {
            // Wrong-kind: file source onto a directory at the resolved target. Container
            // resolution already ran -- if the destination is still a directory at this
            // point, the caller passed an exact path that conflicts with a directory.
            throw new IOException(
                $"Cannot move file '{source.Value}' to '{dest.Value}': a directory exists at the destination. "
              + "Wrong-kind conflicts are not silently resolved. Delete the directory explicitly and retry."
            );
        }

        File.Move(source.Value, dest.Value);
    }

    private static void MoveDirectoryWithPolicy(MPath source, MPath dest, ExistsPolicy policy)
    {
        if (File.Exists(dest.Value)) {
            // D-20: directory source + file destination -> explicit IOException.
            throw new IOException(
                $"Cannot move directory '{source.Value}' to '{dest.Value}': a file exists at the destination. "
              + "Wrong-kind conflicts are not silently resolved. Delete the file explicitly and retry."
            );
        }

        if (Directory.Exists(dest.Value)) {
            // D-19: directory-vs-directory collision -- Fail / Replace / Merge.
            if ((policy & ExistsPolicy.DirectoryReplace) != 0) {
                dest.DeleteDirectory();
                // Fall through to the bare Directory.Move below.
            } else if ((policy & ExistsPolicy.DirectoryMerge) != 0) {
                MergeDirectoryInto(source, dest, policy);
                // After the merge, prune the source root only if it is fully empty. With
                // FileSkip in play, skipped source files survive the merge by design --
                // skip means "do not move this file". A non-empty source root therefore
                // signals "skip happened" and is left in place rather than throwing on a
                // recursive=false delete.
                RemoveIfEmpty(source);

                return;
            } else {
                throw new IOException(
                    $"Cannot move directory '{source.Value}' to '{dest.Value}': destination directory already exists. "
                  + "Pass ExistsPolicy.DirectoryMerge or ExistsPolicy.DirectoryReplace to override."
                );
            }
        }

        dest.EnsureParentExists();
        Directory.Move(source.Value, dest.Value);
    }

    // Recursive merge for the Move side. File-level conflicts inside the merged tree
    // honor the file bits in `policy` via `MoveFileWithPolicy`. The source side is
    // emptied as we walk -- after a successful merge, every subdirectory has been
    // deleted and every file has been moved.
    //
    // FileSkip on a merge leaves the source file in place; that means the source
    // subtree may not be fully empty after this method returns. The caller in
    // MoveDirectoryWithPolicy uses `DeleteDirectory(recursive: false)` on the source
    // root, which throws if any skipped file remains. That is intentional -- callers
    // mixing MergeAndSkip with Move learn loudly that "skip" means "the source survives".
    private static void MergeDirectoryInto(MPath source, MPath dest, ExistsPolicy policy)
    {
        foreach (var srcFile in source.EnumerateFiles("*", SearchOption.TopDirectoryOnly)) {
            var destFile = dest / srcFile.Name;
            MoveFileWithPolicy(srcFile, destFile, policy);
        }

        foreach (var srcSub in source.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)) {
            var destSub = dest / srcSub.Name;
            if (!destSub.DirectoryExists()) {
                destSub.CreateDirectory();
            }

            MergeDirectoryInto(srcSub, destSub, policy);
            // Source subdirectory drained when no skips happened -- remove it so the
            // parent's eventual cleanup sees an empty tree. With FileSkip in play, a
            // subdirectory may still contain skipped files; leave it in place in that case.
            RemoveIfEmpty(srcSub);
        }
    }

    // Best-effort prune. Used after merge-driven moves where FileSkip may have left
    // files behind: deleting the source root unconditionally would throw, which is the
    // wrong shape for a "skip means don't move" contract.
    private static void RemoveIfEmpty(MPath path)
    {
        if (!Directory.Exists(path.Value)) {
            return;
        }

        // EnumerateFileSystemEntries is lazy -- this Any() call short-circuits on the
        // first entry without walking the whole tree.
        if (Directory.EnumerateFileSystemEntries(path.Value).Any()) {
            return;
        }

        Directory.Delete(path.Value, false);
    }

    private static void CopyFileWithPolicy(MPath source, MPath dest, ExistsPolicy policy)
    {
        dest.EnsureParentExists();

        if (File.Exists(dest.Value)) {
            if ((policy & ExistsPolicy.FileSkip) != 0) {
                return;
            }

            if ((policy & ExistsPolicy.FileOverwrite) != 0) {
                // The three-arg File.Copy(overwrite: true) overload is available on every
                // TFM (including netstandard2.1), so no Delete+Copy dance is required.
                File.Copy(source.Value, dest.Value, true);

                return;
            }

            if ((policy & ExistsPolicy.FileOverwriteIfNewer) != 0) {
                if (File.GetLastWriteTimeUtc(source.Value) > File.GetLastWriteTimeUtc(dest.Value)) {
                    File.Copy(source.Value, dest.Value, true);
                }

                return;
            }

            throw new IOException(
                $"Cannot copy file '{source.Value}' to '{dest.Value}': destination file already exists. "
              + "Pass ExistsPolicy.FileOverwrite, ExistsPolicy.FileOverwriteIfNewer, or ExistsPolicy.FileSkip to override."
            );
        }

        if (Directory.Exists(dest.Value)) {
            throw new IOException(
                $"Cannot copy file '{source.Value}' to '{dest.Value}': a directory exists at the destination. "
              + "Wrong-kind conflicts are not silently resolved. Delete the directory explicitly and retry."
            );
        }

        File.Copy(source.Value, dest.Value);
    }

    private static void CopyDirectoryWithPolicy(
        MPath source,
        MPath dest,
        ExistsPolicy policy,
        Func<MPath, bool>? filter
    )
    {
        if (File.Exists(dest.Value)) {
            // D-20 wrong-kind: directory source onto a file destination.
            throw new IOException(
                $"Cannot copy directory '{source.Value}' to '{dest.Value}': a file exists at the destination. "
              + "Wrong-kind conflicts are not silently resolved. Delete the file explicitly and retry."
            );
        }

        if (Directory.Exists(dest.Value)) {
            if ((policy & ExistsPolicy.DirectoryReplace) != 0) {
                dest.DeleteDirectory();
                // Fall through to the fresh-create + recursive walk below.
            } else if ((policy & ExistsPolicy.DirectoryMerge) != 0) {
                CopyDirectoryMergeInto(source, dest, policy, filter);

                return;
            } else {
                throw new IOException(
                    $"Cannot copy directory '{source.Value}' to '{dest.Value}': destination directory already exists. "
                  + "Pass ExistsPolicy.DirectoryMerge or ExistsPolicy.DirectoryReplace to override."
                );
            }
        }

        dest.EnsureParentExists();
        dest.CreateDirectory();
        CopyDirectoryMergeInto(source, dest, policy, filter);
    }

    private static void CopyDirectoryMergeInto(
        MPath source,
        MPath dest,
        ExistsPolicy policy,
        Func<MPath, bool>? filter
    )
    {
        foreach (var srcFile in source.EnumerateFiles("*", SearchOption.TopDirectoryOnly)) {
            if (filter is not null && !filter(srcFile)) {
                continue;
            }

            var destFile = dest / srcFile.Name;
            CopyFileWithPolicy(srcFile, destFile, policy);
        }

        foreach (var srcSub in source.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)) {
            if (filter is not null && !filter(srcSub)) {
                // Skipped directory: do not enter, do not create at the destination.
                continue;
            }

            var destSub = dest / srcSub.Name;
            if (!destSub.DirectoryExists()) {
                destSub.CreateDirectory();
            }

            CopyDirectoryMergeInto(srcSub, destSub, policy, filter);
        }
    }

#endregion
}
