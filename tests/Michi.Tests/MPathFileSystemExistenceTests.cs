using Michi.FileSystem;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Tests cover the two typed existence probes on MPath: each must return true only for the
// matching entry kind and must not throw on missing paths or null input (beyond the documented
// ArgumentNullException contract).
public sealed class MPathFileSystemExistenceTests : IDisposable {
    private readonly string _tempRoot;

    public MPathFileSystemExistenceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"michi-fs-exists-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

    [Fact]
    public void FileExists_returns_true_for_existing_file()
    {
        var filePath = Path.Combine(_tempRoot, "a.txt");
        File.WriteAllText(filePath, "content");

        MPath.From(filePath).FileExists().ShouldBeTrue();
    }

    // A real directory at the path must not report as a file -- File.Exists semantics.
    [Fact]
    public void FileExists_returns_false_for_directory()
    {
        var dirPath = Path.Combine(_tempRoot, "a-dir");
        Directory.CreateDirectory(dirPath);

        MPath.From(dirPath).FileExists().ShouldBeFalse();
    }

    // Probing a path that doesn't exist must be non-throwing -- this is the whole point of a probe.
    [Fact]
    public void FileExists_returns_false_for_missing_path()
    {
        var missing = Path.Combine(_tempRoot, "nope.txt");

        MPath.From(missing).FileExists().ShouldBeFalse();
    }

    [Fact]
    public void DirectoryExists_returns_true_for_existing_directory()
    {
        var dirPath = Path.Combine(_tempRoot, "present");
        Directory.CreateDirectory(dirPath);

        MPath.From(dirPath).DirectoryExists().ShouldBeTrue();
    }

    // A real file at the path must not report as a directory -- Directory.Exists semantics.
    [Fact]
    public void DirectoryExists_returns_false_for_file()
    {
        var filePath = Path.Combine(_tempRoot, "b.txt");
        File.WriteAllText(filePath, "content");

        MPath.From(filePath).DirectoryExists().ShouldBeFalse();
    }

    [Fact]
    public void DirectoryExists_returns_false_for_missing_path()
    {
        var missing = Path.Combine(_tempRoot, "missing-dir");

        MPath.From(missing).DirectoryExists().ShouldBeFalse();
    }

    // Null input goes through ArgumentNullException with paramName = "path". Callers that
    // invoke the extension as a regular static method hit the same contract as the instance
    // form would (null receivers on extension methods throw NullReferenceException at the
    // callsite -- this test covers the static-invocation path).
    [Fact]
    public void FileExists_throws_ArgumentNullException_when_path_is_null()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.FileExists());
        ex.ParamName.ShouldBe("path");
    }

    [Fact]
    public void DirectoryExists_throws_ArgumentNullException_when_path_is_null()
    {
        MPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.DirectoryExists());
        ex.ParamName.ShouldBe("path");
    }
}
