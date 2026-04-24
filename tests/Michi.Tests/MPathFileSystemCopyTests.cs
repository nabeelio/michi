using Michi.Extensions;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Tests cover CopyTo across the same surface as MoveTo (D-14 through D-21) plus the
// optional recursive filter callback (D-22 through D-25).
//
// The shape matches MoveTo: argument validation is eager (before any I/O); destination
// resolution follows cp/mv semantics; policy bits drive per-file behavior on collision;
// directory bits drive Replace / Merge / Fail behavior on directory collision.
//
// CopyTo differs from MoveTo in two ways:
//   1. Source is preserved.
//   2. The optional `filter` parameter (Func<MPath, bool>?) is invoked once per
//      enumerated child during recursive directory copy. Returning false skips the
//      item entirely; the filter is not invoked for flat file-to-file copy.
public sealed class MPathFileSystemCopyTests : IDisposable {
    private readonly string _tempRoot;

    public MPathFileSystemCopyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"michi-fs-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

    private string TempPath(string relative) => Path.Combine(_tempRoot, relative);

    private MPath WriteFile(string relative, string content)
    {
        var full = TempPath(relative);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);

        return MPath.From(full);
    }

    private MPath MakeDir(string relative)
    {
        var full = TempPath(relative);
        Directory.CreateDirectory(full);

        return MPath.From(full);
    }

    [Fact]
    public void CopyTo_throws_eagerly_on_conflicting_file_bits()
    {
        var src = MPath.From(TempPath("does-not-exist-src.txt"));
        var dst = MPath.From(TempPath("does-not-exist-dst.txt"));

        var ex = Should.Throw<ArgumentException>(() => src.CopyTo(
                    dst,
                    ExistsPolicy.FileSkip | ExistsPolicy.FileOverwrite
                )
        );

        ex.ParamName.ShouldBe("policy");
    }

    [Fact]
    public void CopyTo_throws_eagerly_on_conflicting_directory_bits()
    {
        var src = MPath.From(TempPath("does-not-exist-src"));
        var dst = MPath.From(TempPath("does-not-exist-dst"));

        var ex = Should.Throw<ArgumentException>(() => src.CopyTo(
                    dst,
                    ExistsPolicy.DirectoryMerge | ExistsPolicy.DirectoryReplace
                )
        );

        ex.ParamName.ShouldBe("policy");
    }

    [Fact]
    public void CopyTo_throws_ArgumentNullException_on_null_source()
    {
        MPath? src = null;
        var dst = MPath.From(TempPath("dst.txt"));

        var ex = Should.Throw<ArgumentNullException>(() => src!.CopyTo(dst));
        ex.ParamName.ShouldBe("source");
    }

    [Fact]
    public void CopyTo_throws_ArgumentNullException_on_null_destination()
    {
        var src = WriteFile("src.txt", "hi");
        MPath? dst = null;

        var ex = Should.Throw<ArgumentNullException>(() => src.CopyTo(dst!));
        ex.ParamName.ShouldBe("destination");
    }

    [Fact]
    public void CopyTo_resolves_existing_directory_destination_as_container()
    {
        var src = WriteFile("a.txt", "hello");
        var dstDir = MakeDir("out");

        src.CopyTo(dstDir);

        // Source preserved (the defining CopyTo difference vs MoveTo).
        File.Exists(TempPath("a.txt")).ShouldBeTrue();
        File.Exists(TempPath("out/a.txt")).ShouldBeTrue();
        File.ReadAllText(TempPath("out/a.txt")).ShouldBe("hello");
    }

    [Fact]
    public void CopyTo_resolves_nonexistent_destination_as_exact_target()
    {
        var src = WriteFile("a.txt", "hello");
        MakeDir("out");
        var dst = MPath.From(TempPath("out/b.txt"));

        src.CopyTo(dst);

        File.Exists(TempPath("a.txt")).ShouldBeTrue();
        File.Exists(TempPath("out/b.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_preserves_source_after_file_copy()
    {
        var src = WriteFile("a.txt", "hello");
        var dst = MPath.From(TempPath("b.txt"));

        src.CopyTo(dst);

        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(src.Value).ShouldBe("hello");
        File.Exists(dst.Value).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_preserves_source_after_directory_copy()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");
        WriteFile("src-tree/sub/b.txt", "b");

        srcDir.CopyTo(MPath.From(TempPath("dst-tree")));

        // Source still intact.
        Directory.Exists(srcDir.Value).ShouldBeTrue();
        File.Exists(TempPath("src-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("src-tree/sub/b.txt")).ShouldBeTrue();
        // Destination has the copy.
        File.Exists(TempPath("dst-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/sub/b.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_auto_creates_missing_destination_parent()
    {
        var src = WriteFile("a.txt", "hello");
        var dst = MPath.From(TempPath("newparent/target.txt"));

        src.CopyTo(dst);

        Directory.Exists(TempPath("newparent")).ShouldBeTrue();
        File.Exists(TempPath("newparent/target.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_file_Fail_policy_throws_on_collision()
    {
        var src = WriteFile("src.txt", "src content");
        var dst = WriteFile("dst.txt", "dst content");

        Should.Throw<IOException>(() => src.CopyTo(dst));

        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(dst.Value).ShouldBe("dst content");
    }

    [Fact]
    public void CopyTo_file_FileOverwrite_replaces_existing()
    {
        var src = WriteFile("src.txt", "src content");
        var dst = WriteFile("dst.txt", "dst content");

        src.CopyTo(dst, ExistsPolicy.FileOverwrite);

        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(dst.Value).ShouldBe("src content");
    }

    [Fact]
    public void CopyTo_file_FileSkip_leaves_destination_alone()
    {
        var src = WriteFile("src.txt", "src content");
        var dst = WriteFile("dst.txt", "dst content");

        src.CopyTo(dst, ExistsPolicy.FileSkip);

        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(dst.Value).ShouldBe("dst content");
    }

    [Fact]
    public void CopyTo_file_FileOverwriteIfNewer_when_source_newer()
    {
        var src = WriteFile("src.txt", "newer src");
        var dst = WriteFile("dst.txt", "older dst");

        var t0 = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(dst.Value, t0.AddHours(-2));
        File.SetLastWriteTimeUtc(src.Value, t0.AddHours(-1));

        src.CopyTo(dst, ExistsPolicy.FileOverwriteIfNewer);

        File.ReadAllText(dst.Value).ShouldBe("newer src");
    }

    [Fact]
    public void CopyTo_file_FileOverwriteIfNewer_when_source_older()
    {
        var src = WriteFile("src.txt", "older src");
        var dst = WriteFile("dst.txt", "newer dst");

        var t0 = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(src.Value, t0.AddHours(-2));
        File.SetLastWriteTimeUtc(dst.Value, t0.AddHours(-1));

        src.CopyTo(dst, ExistsPolicy.FileOverwriteIfNewer);

        File.ReadAllText(dst.Value).ShouldBe("newer dst");
    }

    [Fact]
    public void CopyTo_directory_Fail_policy_throws_on_collision()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");

        // Container resolution: dst-tree exists, target becomes dst-tree/src-tree.
        // Pre-create that resolved target so dir-vs-dir collision triggers.
        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/preexisting.txt", "pre");

        Should.Throw<IOException>(() => srcDir.CopyTo(MPath.From(TempPath("dst-tree"))));

        // Destination preexisting content untouched.
        File.Exists(TempPath("dst-tree/src-tree/preexisting.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_directory_DirectoryReplace_at_resolved_target_replaces_tree()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "src-a");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/preexisting.txt", "pre");

        srcDir.CopyTo(MPath.From(TempPath("dst-tree")), ExistsPolicy.DirectoryReplace);

        File.Exists(TempPath("dst-tree/src-tree/preexisting.txt")).ShouldBeFalse();
        File.Exists(TempPath("dst-tree/src-tree/a.txt")).ShouldBeTrue();
        // Source preserved.
        Directory.Exists(srcDir.Value).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_directory_DirectoryMerge_merges_file_contents()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "src-a");
        WriteFile("src-tree/sub/c.txt", "src-c");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/b.txt", "dst-b");
        WriteFile("dst-tree/src-tree/sub/d.txt", "dst-d");

        srcDir.CopyTo(MPath.From(TempPath("dst-tree")), ExistsPolicy.DirectoryMerge);

        File.Exists(TempPath("dst-tree/src-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/b.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/sub/c.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/sub/d.txt")).ShouldBeTrue();
        // Source still intact.
        File.Exists(TempPath("src-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("src-tree/sub/c.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_directory_MergeAndOverwrite_overwrites_same_named_files()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/shared.txt", "src-content");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/shared.txt", "dst-content");

        srcDir.CopyTo(MPath.From(TempPath("dst-tree")), ExistsPolicy.MergeAndOverwrite);

        File.ReadAllText(TempPath("dst-tree/src-tree/shared.txt")).ShouldBe("src-content");
        // Source preserved.
        File.ReadAllText(TempPath("src-tree/shared.txt")).ShouldBe("src-content");
    }

    [Fact]
    public void CopyTo_directory_MergeAndSkip_preserves_existing_dest_files()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/shared.txt", "src-content");
        WriteFile("src-tree/unique.txt", "unique-src");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/shared.txt", "dst-content");

        srcDir.CopyTo(MPath.From(TempPath("dst-tree")), ExistsPolicy.MergeAndSkip);

        File.ReadAllText(TempPath("dst-tree/src-tree/shared.txt")).ShouldBe("dst-content");
        File.ReadAllText(TempPath("dst-tree/src-tree/unique.txt")).ShouldBe("unique-src");
        // Source intact (CopyTo never removes source).
        File.Exists(TempPath("src-tree/shared.txt")).ShouldBeTrue();
        File.Exists(TempPath("src-tree/unique.txt")).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_directory_MergeAndOverwriteIfNewer_only_overwrites_newer_sources()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/newer.txt", "src-newer");
        WriteFile("src-tree/older.txt", "src-older");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/newer.txt", "dst-newer");
        WriteFile("dst-tree/src-tree/older.txt", "dst-older");

        var t0 = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(TempPath("dst-tree/src-tree/newer.txt"), t0.AddHours(-2));
        File.SetLastWriteTimeUtc(TempPath("src-tree/newer.txt"), t0.AddHours(-1));
        File.SetLastWriteTimeUtc(TempPath("src-tree/older.txt"), t0.AddHours(-2));
        File.SetLastWriteTimeUtc(TempPath("dst-tree/src-tree/older.txt"), t0.AddHours(-1));

        srcDir.CopyTo(MPath.From(TempPath("dst-tree")), ExistsPolicy.MergeAndOverwriteIfNewer);

        File.ReadAllText(TempPath("dst-tree/src-tree/newer.txt")).ShouldBe("src-newer");
        File.ReadAllText(TempPath("dst-tree/src-tree/older.txt")).ShouldBe("dst-older");
    }

    [Fact]
    public void CopyTo_directory_into_existing_file_throws_IOException()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");

        MakeDir("dst-parent");
        WriteFile("dst-parent/src-tree", "i am a file");

        var ex = Should.Throw<IOException>(() => srcDir.CopyTo(MPath.From(TempPath("dst-parent"))));

        ex.Message.ShouldContain(srcDir.Value);
        // Source preserved.
        Directory.Exists(srcDir.Value).ShouldBeTrue();
    }

    [Fact]
    public void CopyTo_same_path_is_noop()
    {
        var src = WriteFile("a.txt", "hello");
        var dst = MPath.From(src.Value);

        Should.NotThrow(() => src.CopyTo(dst));
        File.ReadAllText(src.Value).ShouldBe("hello");
    }

    // Filter excludes a file by returning false. Recursive directory copy enumerates
    // both files and directories; for each item, the filter decides include vs skip.
    [Fact]
    public void CopyTo_filter_excludes_file_by_returning_false()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a-content");
        WriteFile("src-tree/b.log", "b-content");

        srcDir.CopyTo(
            MPath.From(TempPath("dst-tree")),
            filter: p => p.Extension != ".log"
        );

        File.Exists(TempPath("dst-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/b.log")).ShouldBeFalse();
    }

    // Filtering a directory skips its entire subtree -- the traversal does not enter
    // the rejected directory at all.
    [Fact]
    public void CopyTo_filter_excludes_subdirectory_and_its_contents()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/src/z.txt", "keep me");
        WriteFile("src-tree/build/x/y.txt", "skip me");

        srcDir.CopyTo(
            MPath.From(TempPath("dst-tree")),
            filter: p => p.Name != "build"
        );

        File.Exists(TempPath("dst-tree/src/z.txt")).ShouldBeTrue();
        Directory.Exists(TempPath("dst-tree/build")).ShouldBeFalse();
        File.Exists(TempPath("dst-tree/build/x/y.txt")).ShouldBeFalse();
    }

    // Flat file-to-file copy: filter must NOT be invoked.
    [Fact]
    public void CopyTo_filter_is_not_invoked_for_flat_file_copy()
    {
        var src = WriteFile("a.txt", "hello");
        var invocations = 0;

        src.CopyTo(
            MPath.From(TempPath("b.txt")),
            filter: _ => {
                Interlocked.Increment(ref invocations);

                return true;
            }
        );

        invocations.ShouldBe(0);
        File.Exists(TempPath("b.txt")).ShouldBeTrue();
    }

    // Filter receives the SOURCE path of each enumerated entry, not a computed
    // destination path. Verify by asserting the argument is rooted at the source tree.
    [Fact]
    public void CopyTo_filter_receives_source_paths_not_destination_paths()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");
        WriteFile("src-tree/sub/b.txt", "b");

        var seen = new List<MPath>();

        srcDir.CopyTo(
            MPath.From(TempPath("dst-tree")),
            filter: p => {
                seen.Add(p);

                return true;
            }
        );

        // Every observed argument starts at the source root, never at the destination.
        seen.ShouldNotBeEmpty();
        foreach (var p in seen) {
            p.Value.ShouldStartWith(srcDir.Value);
            p.Value.ShouldNotStartWith(TempPath("dst-tree"));
        }
    }

    // Filter applies to directories: a directory the filter rejects is not created in
    // the destination, even if it would have been created as part of structural walk.
    [Fact]
    public void CopyTo_filter_applies_to_directories()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/keep/a.txt", "keep-a");
        WriteFile("src-tree/skip/b.txt", "skip-b");

        srcDir.CopyTo(
            MPath.From(TempPath("dst-tree")),
            filter: p => p.Name != "skip"
        );

        Directory.Exists(TempPath("dst-tree/keep")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/keep/a.txt")).ShouldBeTrue();
        Directory.Exists(TempPath("dst-tree/skip")).ShouldBeFalse();
    }
}
