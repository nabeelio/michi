using Michi.FileSystem;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Tests cover DeleteFile and DeleteDirectory. Both methods are idempotent when the
// target is missing (D-11, D-12) and throw explicit IOExceptions on wrong-kind
// targets (D-13) rather than silently succeeding or producing a confusing BCL error.
public sealed class MPathFileSystemDeletionTests : IDisposable {
    private readonly string _tempRoot;

    public MPathFileSystemDeletionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"michi-fs-delete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

#region DeleteFile

    [Fact]
    public void DeleteFile_removes_existing_file()
    {
        var filePath = Path.Combine(_tempRoot, "remove-me.txt");
        File.WriteAllText(filePath, "content");
        var target = MPath.From(filePath);

        target.DeleteFile();

        File.Exists(filePath).ShouldBeFalse();
    }

    // Idempotency: missing file is a no-op (D-11).
    [Fact]
    public void DeleteFile_is_noop_when_file_missing()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "does-not-exist.txt"));

        Should.NotThrow(() => target.DeleteFile());
    }

    // Wrong-kind guard: a directory at the target path must produce an explicit
    // IOException naming the path and pointing at DeleteDirectory as the remedy.
    [Fact]
    public void DeleteFile_throws_IOException_when_directory_at_path()
    {
        var dirPath = Path.Combine(_tempRoot, "i-am-a-directory");
        Directory.CreateDirectory(dirPath);
        var target = MPath.From(dirPath);

        var ex = Should.Throw<IOException>(() => target.DeleteFile());

        ex.Message.ShouldContain(dirPath);
        ex.Message.ShouldContain("DeleteDirectory");

        // The directory must NOT be deleted as a side effect -- explicit D-13 check.
        Directory.Exists(dirPath).ShouldBeTrue();
    }

    [Fact]
    public void DeleteFile_throws_ArgumentNullException_on_null_path()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.DeleteFile());
        ex.ParamName.ShouldBe("path");
    }

#endregion

#region DeleteDirectory

    [Fact]
    public void DeleteDirectory_removes_existing_empty_directory()
    {
        var dirPath = Path.Combine(_tempRoot, "empty");
        Directory.CreateDirectory(dirPath);
        var target = MPath.From(dirPath);

        target.DeleteDirectory();

        Directory.Exists(dirPath).ShouldBeFalse();
    }

    // Default recursive=true: populated subtree is removed in its entirety.
    [Fact]
    public void DeleteDirectory_removes_populated_directory_recursively_by_default()
    {
        var dirPath = Path.Combine(_tempRoot, "populated");
        Directory.CreateDirectory(Path.Combine(dirPath, "child", "grandchild"));
        File.WriteAllText(Path.Combine(dirPath, "a.txt"), "a");
        File.WriteAllText(Path.Combine(dirPath, "child", "b.txt"), "b");
        File.WriteAllText(Path.Combine(dirPath, "child", "grandchild", "c.txt"), "c");
        var target = MPath.From(dirPath);

        target.DeleteDirectory();

        Directory.Exists(dirPath).ShouldBeFalse();
    }

    [Fact]
    public void DeleteDirectory_non_recursive_on_empty_directory_succeeds()
    {
        var dirPath = Path.Combine(_tempRoot, "empty-nonrec");
        Directory.CreateDirectory(dirPath);
        var target = MPath.From(dirPath);

        target.DeleteDirectory(false);

        Directory.Exists(dirPath).ShouldBeFalse();
    }

    // BCL contract: Directory.Delete(path, false) on non-empty throws IOException.
    // We do not assert message wording (it belongs to the BCL, not to Michi).
    [Fact]
    public void DeleteDirectory_non_recursive_on_non_empty_throws_IOException()
    {
        var dirPath = Path.Combine(_tempRoot, "nonempty-nonrec");
        Directory.CreateDirectory(dirPath);
        File.WriteAllText(Path.Combine(dirPath, "a.txt"), "a");
        var target = MPath.From(dirPath);

        Should.Throw<IOException>(() => target.DeleteDirectory(false));

        // Directory must still be present -- failure is clean.
        Directory.Exists(dirPath).ShouldBeTrue();
    }

    // Idempotency for both recursive flavours (D-12).
    [Fact]
    public void DeleteDirectory_is_noop_when_directory_missing_recursive_true()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "missing-dir"));

        Should.NotThrow(() => target.DeleteDirectory());
    }

    [Fact]
    public void DeleteDirectory_is_noop_when_directory_missing_recursive_false()
    {
        var target = MPath.From(Path.Combine(_tempRoot, "missing-dir-nonrec"));

        Should.NotThrow(() => target.DeleteDirectory(false));
    }

    // Wrong-kind guard: a file at the target path produces an explicit IOException
    // naming the path and pointing at DeleteFile as the remedy.
    [Fact]
    public void DeleteDirectory_throws_IOException_when_file_at_path()
    {
        var filePath = Path.Combine(_tempRoot, "i-am-a-file.txt");
        File.WriteAllText(filePath, "content");
        var target = MPath.From(filePath);

        var ex = Should.Throw<IOException>(() => target.DeleteDirectory());

        ex.Message.ShouldContain(filePath);
        ex.Message.ShouldContain("DeleteFile");

        // The file must NOT be deleted as a side effect -- explicit D-13 check.
        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public void DeleteDirectory_throws_ArgumentNullException_on_null_path()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.DeleteDirectory());
        ex.ParamName.ShouldBe("path");
    }

#endregion
}
