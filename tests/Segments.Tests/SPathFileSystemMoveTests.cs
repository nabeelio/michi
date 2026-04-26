using Segments.FileSystem;
using Shouldly;
using Xunit;

namespace Segments.Tests;

// Tests cover MoveTo across the full ExistsPolicy surface (D-14 through D-21), the
// cp/mv destination resolution rule (D-17 + D-18: existing destination directory acts
// as a parent container; otherwise destination is the exact target), the wrong-kind
// guard (D-20: directory source onto an existing file throws), the same-path no-op,
// and the auto-parent-create on the destination side (ROADMAP success criterion 5).
//
// Argument validation runs BEFORE any filesystem I/O (ROADMAP success criterion 4):
// passing conflicting policy bits throws ArgumentException before the source or
// destination paths are even probed.
public sealed class SPathFileSystemMoveTests : IDisposable {
    private readonly string _tempRoot;

    public SPathFileSystemMoveTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"segments-fs-move-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

    private string TempPath(string relative) => Path.Combine(_tempRoot, relative);

    private SPath WriteFile(string relative, string content)
    {
        var full = TempPath(relative);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);

        return SPath.From(full);
    }

    private SPath MakeDir(string relative)
    {
        var full = TempPath(relative);
        Directory.CreateDirectory(full);

        return SPath.From(full);
    }

    // Conflicting file bits must throw ArgumentException at the call site, before any
    // probe of the source path. We use a source path that does not exist on disk to
    // prove the throw came from policy validation, not from a missing-source error.
    [Fact]
    public void MoveTo_throws_eagerly_on_conflicting_file_bits()
    {
        var src = SPath.From(TempPath("does-not-exist-src.txt"));
        var dst = SPath.From(TempPath("does-not-exist-dst.txt"));

        var ex = Should.Throw<ArgumentException>(() => src.MoveTo(
                    dst,
                    ExistsPolicy.FileSkip | ExistsPolicy.FileOverwrite
                )
        );

        ex.ParamName.ShouldBe("policy");
        ex.Message.ShouldContain("FileSkip");
        ex.Message.ShouldContain("FileOverwrite");
    }

    [Fact]
    public void MoveTo_throws_eagerly_on_conflicting_directory_bits()
    {
        var src = SPath.From(TempPath("does-not-exist-src"));
        var dst = SPath.From(TempPath("does-not-exist-dst"));

        var ex = Should.Throw<ArgumentException>(() => src.MoveTo(
                    dst,
                    ExistsPolicy.DirectoryMerge | ExistsPolicy.DirectoryReplace
                )
        );

        ex.ParamName.ShouldBe("policy");
        ex.Message.ShouldContain("DirectoryMerge");
        ex.Message.ShouldContain("DirectoryReplace");
    }

    [Fact]
    public void MoveTo_throws_ArgumentNullException_on_null_source()
    {
        SPath? src = null;
        var dst = SPath.From(TempPath("dst.txt"));

        var ex = Should.Throw<ArgumentNullException>(() => src!.MoveTo(dst));
        ex.ParamName.ShouldBe("source");
    }

    [Fact]
    public void MoveTo_throws_ArgumentNullException_on_null_destination()
    {
        var src = WriteFile("src.txt", "hi");
        SPath? dst = null;

        var ex = Should.Throw<ArgumentNullException>(() => src.MoveTo(dst!));
        ex.ParamName.ShouldBe("destination");
    }

    // Existing destination directory is treated as a parent container -- the resolved
    // target becomes destination/source.Name (cp/mv semantics).
    [Fact]
    public void MoveTo_resolves_existing_directory_destination_as_container()
    {
        var src = WriteFile("a.txt", "hello");
        var dstDir = MakeDir("out");

        src.MoveTo(dstDir);

        File.Exists(TempPath("a.txt")).ShouldBeFalse();
        File.Exists(TempPath("out/a.txt")).ShouldBeTrue();
        File.ReadAllText(TempPath("out/a.txt")).ShouldBe("hello");
    }

    [Fact]
    public void MoveTo_resolves_nonexistent_destination_as_exact_target()
    {
        var src = WriteFile("a.txt", "hello");
        // out/ exists, but out/b.txt does not -- treated as exact target.
        MakeDir("out");
        var dst = SPath.From(TempPath("out/b.txt"));

        src.MoveTo(dst);

        File.Exists(TempPath("a.txt")).ShouldBeFalse();
        File.Exists(TempPath("out/b.txt")).ShouldBeTrue();
        File.ReadAllText(TempPath("out/b.txt")).ShouldBe("hello");
    }

    // The parent of the resolved destination is auto-created if missing.
    [Fact]
    public void MoveTo_auto_creates_missing_destination_parent()
    {
        var src = WriteFile("a.txt", "hello");
        // newparent/ does NOT exist yet; MoveTo should create it.
        var dst = SPath.From(TempPath("newparent/target.txt"));

        src.MoveTo(dst);

        Directory.Exists(TempPath("newparent")).ShouldBeTrue();
        File.Exists(TempPath("newparent/target.txt")).ShouldBeTrue();
    }

    [Fact]
    public void MoveTo_file_Fail_policy_throws_on_collision()
    {
        var src = WriteFile("src.txt", "src content");
        var dst = WriteFile("dst.txt", "dst content");

        Should.Throw<IOException>(() => src.MoveTo(dst));

        // Both files left in place on failure.
        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(dst.Value).ShouldBe("dst content");
    }

    [Fact]
    public void MoveTo_file_FileOverwrite_replaces_existing()
    {
        var src = WriteFile("src.txt", "src content");
        var dst = WriteFile("dst.txt", "dst content");

        src.MoveTo(dst, ExistsPolicy.FileOverwrite);

        File.Exists(src.Value).ShouldBeFalse();
        File.ReadAllText(dst.Value).ShouldBe("src content");
    }

    // FileSkip on Move: destination is left alone AND source is left in place
    // (skip means "don't move this file"). Documented as such in MoveTo's XML doc.
    [Fact]
    public void MoveTo_file_FileSkip_leaves_source_and_destination_alone()
    {
        var src = WriteFile("src.txt", "src content");
        var dst = WriteFile("dst.txt", "dst content");

        src.MoveTo(dst, ExistsPolicy.FileSkip);

        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(src.Value).ShouldBe("src content");
        File.ReadAllText(dst.Value).ShouldBe("dst content");
    }

    [Fact]
    public void MoveTo_file_FileOverwriteIfNewer_when_source_newer()
    {
        var src = WriteFile("src.txt", "newer src");
        var dst = WriteFile("dst.txt", "older dst");

        // Force timestamps so source > destination.
        var t0 = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(dst.Value, t0.AddHours(-2));
        File.SetLastWriteTimeUtc(src.Value, t0.AddHours(-1));

        src.MoveTo(dst, ExistsPolicy.FileOverwriteIfNewer);

        File.Exists(src.Value).ShouldBeFalse();
        File.ReadAllText(dst.Value).ShouldBe("newer src");
    }

    [Fact]
    public void MoveTo_file_FileOverwriteIfNewer_when_source_older()
    {
        var src = WriteFile("src.txt", "older src");
        var dst = WriteFile("dst.txt", "newer dst");

        var t0 = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(src.Value, t0.AddHours(-2));
        File.SetLastWriteTimeUtc(dst.Value, t0.AddHours(-1));

        src.MoveTo(dst, ExistsPolicy.FileOverwriteIfNewer);

        // Source older -> skip semantics. Source must remain in place (mirrors FileSkip).
        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(dst.Value).ShouldBe("newer dst");
    }

    [Fact]
    public void MoveTo_directory_Fail_policy_throws_on_collision()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");
        var dstDir = MakeDir("dst-tree");
        WriteFile("dst-tree/b.txt", "b");

        // Without container-resolution: srcDir.MoveTo(dstDir) where dstDir exists as a
        // directory would normally resolve to dstDir/srcDir.Name. To exercise the dir-vs-dir
        // collision path we move into a path that already has a directory at the resolved
        // target.
        var resolved = SPath.From(Path.Combine(dstDir.Value, srcDir.Name));
        Directory.CreateDirectory(resolved.Value);
        File.WriteAllText(Path.Combine(resolved.Value, "preexisting.txt"), "pre");

        Should.Throw<IOException>(() => srcDir.MoveTo(dstDir));

        // Source must still be present on failure.
        Directory.Exists(srcDir.Value).ShouldBeTrue();
    }

    [Fact]
    public void MoveTo_directory_DirectoryReplace_removes_existing_then_moves()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");
        WriteFile("src-tree/sub/b.txt", "b");

        // Build a destination that already contains different content; DirectoryReplace
        // must obliterate it.
        var dstDir = MakeDir("dst-tree");
        WriteFile("dst-tree/preexisting.txt", "pre");

        srcDir.MoveTo(dstDir, ExistsPolicy.DirectoryReplace);

        // After Replace, dstDir should contain only the source's content.
        // The container-resolution rule (D-17) applies: dst-tree exists, so the move
        // resolves to dst-tree/src-tree. dst-tree itself is the existing destination
        // directory; Replace deletes the resolved target (dst-tree/src-tree) if present.
        // In this scenario the resolved target dst-tree/src-tree did not exist, so
        // Replace is a no-op on the destination side -- the move just goes through.
        // Verify:
        Directory.Exists(srcDir.Value).ShouldBeFalse();
        File.Exists(TempPath("dst-tree/src-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/sub/b.txt")).ShouldBeTrue();
        // Pre-existing dst-tree content untouched (Replace targeted the resolved dest,
        // not dst-tree itself).
        File.Exists(TempPath("dst-tree/preexisting.txt")).ShouldBeTrue();
    }

    // True dir-vs-dir Replace: resolved destination is itself an existing directory.
    [Fact]
    public void MoveTo_directory_DirectoryReplace_at_resolved_target_replaces_tree()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "src-a");

        // Build dst-tree/src-tree as the resolved target so Replace acts on it.
        MakeDir("dst-tree");
        var resolvedTarget = MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/preexisting.txt", "pre");

        srcDir.MoveTo(SPath.From(TempPath("dst-tree")), ExistsPolicy.DirectoryReplace);

        // After Replace at resolved target: pre-existing content gone, source content present.
        File.Exists(TempPath("dst-tree/src-tree/preexisting.txt")).ShouldBeFalse();
        File.Exists(TempPath("dst-tree/src-tree/a.txt")).ShouldBeTrue();
        File.ReadAllText(TempPath("dst-tree/src-tree/a.txt")).ShouldBe("src-a");
        // Source removed.
        Directory.Exists(srcDir.Value).ShouldBeFalse();
        _ = resolvedTarget;
    }

    [Fact]
    public void MoveTo_directory_DirectoryMerge_merges_file_contents()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "src-a");
        WriteFile("src-tree/sub/c.txt", "src-c");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/b.txt", "dst-b");
        WriteFile("dst-tree/src-tree/sub/d.txt", "dst-d");

        srcDir.MoveTo(SPath.From(TempPath("dst-tree")), ExistsPolicy.DirectoryMerge);

        // All four files present after merge.
        File.Exists(TempPath("dst-tree/src-tree/a.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/b.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/sub/c.txt")).ShouldBeTrue();
        File.Exists(TempPath("dst-tree/src-tree/sub/d.txt")).ShouldBeTrue();
        // Source root removed after merge.
        Directory.Exists(srcDir.Value).ShouldBeFalse();
    }

    [Fact]
    public void MoveTo_directory_MergeAndOverwrite_overwrites_same_named_files()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/shared.txt", "src-content");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/shared.txt", "dst-content");

        srcDir.MoveTo(SPath.From(TempPath("dst-tree")), ExistsPolicy.MergeAndOverwrite);

        File.ReadAllText(TempPath("dst-tree/src-tree/shared.txt")).ShouldBe("src-content");
        Directory.Exists(srcDir.Value).ShouldBeFalse();
    }

    [Fact]
    public void MoveTo_directory_MergeAndSkip_preserves_existing_dest_files()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/shared.txt", "src-content");
        WriteFile("src-tree/unique.txt", "unique-src");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/shared.txt", "dst-content");

        srcDir.MoveTo(SPath.From(TempPath("dst-tree")), ExistsPolicy.MergeAndSkip);

        // Destination shared file untouched; unique source file moved through.
        File.ReadAllText(TempPath("dst-tree/src-tree/shared.txt")).ShouldBe("dst-content");
        File.ReadAllText(TempPath("dst-tree/src-tree/unique.txt")).ShouldBe("unique-src");
        // Source's "shared.txt" file remains because Skip means "don't move".
        File.Exists(TempPath("src-tree/shared.txt")).ShouldBeTrue();
    }

    [Fact]
    public void MoveTo_directory_MergeAndOverwriteIfNewer_only_overwrites_newer_sources()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/newer.txt", "src-newer");
        WriteFile("src-tree/older.txt", "src-older");

        MakeDir("dst-tree");
        MakeDir("dst-tree/src-tree");
        WriteFile("dst-tree/src-tree/newer.txt", "dst-newer");
        WriteFile("dst-tree/src-tree/older.txt", "dst-older");

        var t0 = DateTime.UtcNow;
        // newer.txt: source > destination -> overwrite
        File.SetLastWriteTimeUtc(TempPath("dst-tree/src-tree/newer.txt"), t0.AddHours(-2));
        File.SetLastWriteTimeUtc(TempPath("src-tree/newer.txt"), t0.AddHours(-1));
        // older.txt: source < destination -> skip
        File.SetLastWriteTimeUtc(TempPath("src-tree/older.txt"), t0.AddHours(-2));
        File.SetLastWriteTimeUtc(TempPath("dst-tree/src-tree/older.txt"), t0.AddHours(-1));

        srcDir.MoveTo(SPath.From(TempPath("dst-tree")), ExistsPolicy.MergeAndOverwriteIfNewer);

        File.ReadAllText(TempPath("dst-tree/src-tree/newer.txt")).ShouldBe("src-newer");
        File.ReadAllText(TempPath("dst-tree/src-tree/older.txt")).ShouldBe("dst-older");
    }

    [Fact]
    public void MoveTo_directory_into_existing_file_throws_IOException()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");

        // Build dst as a file at the resolved target path (dst-parent/src-tree).
        MakeDir("dst-parent");
        WriteFile("dst-parent/src-tree", "i am a file");

        var ex = Should.Throw<IOException>(() => srcDir.MoveTo(SPath.From(TempPath("dst-parent"))));

        ex.Message.ShouldContain(srcDir.Value);
        // Source untouched.
        Directory.Exists(srcDir.Value).ShouldBeTrue();
    }

    // Documented non-wrong-kind: file source onto an existing-directory destination is
    // container resolution (D-17), NOT a wrong-kind error.
    [Fact]
    public void MoveTo_file_onto_existing_directory_target_does_container_resolution_not_wrong_kind()
    {
        var src = WriteFile("a.txt", "hello");
        var dstDir = MakeDir("out");

        Should.NotThrow(() => src.MoveTo(dstDir));
        File.Exists(TempPath("out/a.txt")).ShouldBeTrue();
    }

    [Fact]
    public void MoveTo_same_path_is_noop()
    {
        var src = WriteFile("a.txt", "hello");
        var dst = SPath.From(src.Value);

        Should.NotThrow(() => src.MoveTo(dst));
        File.Exists(src.Value).ShouldBeTrue();
        File.ReadAllText(src.Value).ShouldBe("hello");
    }

    [Fact]
    public void MoveTo_same_directory_path_is_noop()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");

        Should.NotThrow(() => srcDir.MoveTo(SPath.From(srcDir.Value)));

        Directory.Exists(srcDir.Value).ShouldBeTrue();
        File.Exists(TempPath("src-tree/a.txt")).ShouldBeTrue();
        Directory.Exists(TempPath("src-tree/src-tree")).ShouldBeFalse();
    }

    [Fact]
    public void MoveTo_directory_rejects_destination_inside_source()
    {
        var srcDir = MakeDir("src-tree");
        WriteFile("src-tree/a.txt", "a");

        var ex = Should.Throw<IOException>(() => srcDir.MoveTo(SPath.From(TempPath("src-tree/child"))));

        ex.Message.ShouldContain("inside the source directory");
        Directory.Exists(srcDir.Value).ShouldBeTrue();
        Directory.Exists(TempPath("src-tree/child")).ShouldBeFalse();
    }
}
