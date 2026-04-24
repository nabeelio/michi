using Michi.Extensions;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Tests cover the three creation-related extension methods: CreateDirectory (idempotent,
// non-destructive), EnsureParentExists (parent-chain only), CreateOrClearDirectory
// (destructive, clears in place, rejects files at the target). All three are fluent --
// they return the same MPath instance so callers can chain further operations.
public sealed class MPathFileSystemCreationTests : IDisposable {
    private readonly string _tempRoot;

    public MPathFileSystemCreationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"michi-fs-create-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

#region CreateDirectory

    [Fact]
    public void CreateDirectory_creates_missing_directory()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "new-dir"));

        target.CreateDirectory();

        Directory.Exists(target.Value).ShouldBeTrue();
    }

    // Idempotency: calling a second time on an already-existing directory must not throw
    // and must leave the directory in place (Directory.CreateDirectory BCL semantics).
    [Fact]
    public void CreateDirectory_is_idempotent()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "twice"));

        target.CreateDirectory();
        Should.NotThrow(() => target.CreateDirectory());

        Directory.Exists(target.Value).ShouldBeTrue();
    }

    // Intermediate parents must be created automatically -- matches Directory.CreateDirectory.
    [Fact]
    public void CreateDirectory_creates_intermediate_parents()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "a", "b", "c"));

        target.CreateDirectory();

        Directory.Exists(Path.Combine(_tempRoot, "a")).ShouldBeTrue();
        Directory.Exists(Path.Combine(_tempRoot, "a", "b")).ShouldBeTrue();
        Directory.Exists(target.Value).ShouldBeTrue();
    }

    // Fluent-chaining contract: the returned MPath is the same reference as the input.
    [Fact]
    public void CreateDirectory_returns_same_instance()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "same-ref"));

        var returned = target.CreateDirectory();

        ReferenceEquals(target, returned).ShouldBeTrue();
    }

    // File-at-target is an IOException from Directory.CreateDirectory -- we do not wrap or
    // suppress it, since the BCL message is adequate and the exception type is stable.
    [Fact]
    public void CreateDirectory_on_existing_file_throws_IOException()
    {
        var filePath = Path.Combine(_tempRoot, "conflict.txt");
        File.WriteAllText(filePath, "content");

        var target = MPath.From(filePath);

        Should.Throw<IOException>(() => target.CreateDirectory());
    }

    [Fact]
    public void CreateDirectory_throws_ArgumentNullException_on_null_path()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.CreateDirectory());
        ex.ParamName.ShouldBe("path");
    }

#endregion

#region EnsureParentExists

    [Fact]
    public void EnsureParentExists_creates_parent_only()
    {
        var parent = Path.Combine(_tempRoot, "new-parent");
        var child = MPath.From(Path.Combine(parent, "child.txt"));

        child.EnsureParentExists();

        Directory.Exists(parent).ShouldBeTrue();
        File.Exists(child.Value).ShouldBeFalse();
        Directory.Exists(child.Value).ShouldBeFalse();
    }

    // Already-present parent is a no-op (Directory.CreateDirectory is idempotent).
    [Fact]
    public void EnsureParentExists_is_noop_when_parent_already_exists()
    {
        var child = MPath.From(Path.Combine(_tempRoot, "child.txt"));

        Should.NotThrow(() => child.EnsureParentExists());

        Directory.Exists(_tempRoot).ShouldBeTrue();
    }

    // A root path has no parent; EnsureParentExists must treat that as a success no-op
    // rather than letting MPath.Parent throw NoParentException.
    [Fact]
    public void EnsureParentExists_is_noop_at_root()
    {
        var rootString = Path.GetPathRoot(Environment.SystemDirectory);
        rootString.ShouldNotBeNull();

        var root = MPath.From(rootString);

        Should.NotThrow(() => root.EnsureParentExists());
    }

    [Fact]
    public void EnsureParentExists_returns_same_instance()
    {
        var child = MPath.From(Path.Combine(_tempRoot, "child.txt"));

        var returned = child.EnsureParentExists();

        ReferenceEquals(child, returned).ShouldBeTrue();
    }

    [Fact]
    public void EnsureParentExists_throws_ArgumentNullException_on_null_path()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.EnsureParentExists());
        ex.ParamName.ShouldBe("path");
    }

#endregion

#region CreateOrClearDirectory

    [Fact]
    public void CreateOrClearDirectory_creates_missing_directory()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "fresh"));

        target.CreateOrClearDirectory();

        Directory.Exists(target.Value).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(target.Value).ShouldBeEmpty();
    }

    [Fact]
    public void CreateOrClearDirectory_clears_existing_files()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "has-files"));
        Directory.CreateDirectory(target.Value);
        File.WriteAllText(Path.Combine(target.Value, "a.txt"), "a");
        File.WriteAllText(Path.Combine(target.Value, "b.txt"), "b");

        target.CreateOrClearDirectory();

        Directory.Exists(target.Value).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(target.Value).ShouldBeEmpty();
    }

    // Recursive clear: subdirectories and their contents must be removed so the result is
    // truly empty. Verifies D-05.
    [Fact]
    public void CreateOrClearDirectory_clears_nested_subdirectories_recursively()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "nested"));
        Directory.CreateDirectory(Path.Combine(target.Value, "child", "grandchild"));
        File.WriteAllText(Path.Combine(target.Value, "root.txt"), "root");
        File.WriteAllText(Path.Combine(target.Value, "child", "child.txt"), "child");
        File.WriteAllText(Path.Combine(target.Value, "child", "grandchild", "leaf.txt"), "leaf");

        target.CreateOrClearDirectory();

        Directory.Exists(target.Value).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(target.Value).ShouldBeEmpty();
    }

    // D-04 behavioral check: after clear the directory must still exist and be empty. A
    // strict inode-identity assertion would require P/Invoke; on .NET we approximate by
    // requiring the directory to be a continuously-valid handle -- i.e. still present
    // immediately after the call with no gap. The stronger contract (root is NOT delete-
    // and-recreated) is enforced in the source by not calling Directory.Delete on the root
    // and verified in acceptance by grep.
    [Fact]
    public void CreateOrClearDirectory_preserves_root_directory_identity()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "keep-root"));
        Directory.CreateDirectory(target.Value);
        File.WriteAllText(Path.Combine(target.Value, "file.txt"), "content");

        var before = new DirectoryInfo(target.Value).CreationTimeUtc;

        target.CreateOrClearDirectory();

        var after = new DirectoryInfo(target.Value).CreationTimeUtc;

        Directory.Exists(target.Value).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(target.Value).ShouldBeEmpty();
        // Matching creation times is a best-effort proxy for "same directory" -- a
        // delete-and-recreate cycle would typically produce a new creation timestamp on
        // platforms that track it (Windows/macOS). On some Linux filesystems ctime is not
        // tracked or is reused; the main guarantee is enforced by source-level acceptance
        // criteria (no Directory.Delete on the root).
        after.ShouldBe(before);
    }

    [Fact]
    public void CreateOrClearDirectory_throws_when_file_exists_at_path()
    {
        var filePath = Path.Combine(_tempRoot, "i-am-a-file.txt");
        File.WriteAllText(filePath, "content");

        var target = MPath.From(filePath);

        var ex = Should.Throw<IOException>(() => target.CreateOrClearDirectory());

        ex.Message.ShouldContain(filePath);
        ex.Message.ShouldContain("file");

        // The file must not be replaced or deleted -- explicit check per D-06.
        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public void CreateOrClearDirectory_returns_same_instance()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "same-ref-clear"));

        var returned = target.CreateOrClearDirectory();

        ReferenceEquals(target, returned).ShouldBeTrue();
    }

    [Fact]
    public void CreateOrClearDirectory_throws_ArgumentNullException_on_null_path()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.CreateOrClearDirectory());
        ex.ParamName.ShouldBe("path");
    }

#endregion
}
