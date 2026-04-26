using Segments.FileSystem;
using Shouldly;
using Xunit;

namespace Segments.Tests;

// Tests cover the two System.IO interop conversions: ToFileInfo and ToDirectoryInfo. The
// methods are thin delegations (new FileInfo(path.Value), new DirectoryInfo(path.Value))
// but the round-trip correctness matters -- constructing an SPath from the returned
// FullName must produce an SPath equal to the original on every TFM and platform.
public sealed class SPathFileSystemInteropTests : IDisposable {
    private readonly string _tempRoot;

    public SPathFileSystemInteropTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"segments-fs-interop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

#region ToFileInfo

    [Fact]
    public void ToFileInfo_returns_FileInfo_with_matching_FullName()
    {
        var filePath = Path.Combine(_tempRoot, "existing.txt");
        File.WriteAllText(filePath, "content");

        var path = SPath.From(filePath);

        var info = path.ToFileInfo();

        info.ShouldNotBeNull();
        info.FullName.ShouldBe(path.Value);
    }

    // The file need not exist -- matches `new FileInfo(path)` BCL semantics. Construction
    // is cheap and non-I/O; callers can probe `Exists`, `Create()`, etc. afterwards.
    [Fact]
    public void ToFileInfo_does_not_require_file_to_exist()
    {
        var missingPath = Path.Combine(_tempRoot, "not-here.txt");
        var path = SPath.From(missingPath);

        var info = Should.NotThrow(() => path.ToFileInfo());

        info.Exists.ShouldBeFalse();
        info.FullName.ShouldBe(path.Value);
    }

    // Round-trip: SPath.From(info.FullName) must produce an SPath equal to the original.
    // Uses the existing host-OS comparer semantics (case-insensitive on Windows/macOS,
    // case-sensitive on Linux) via the SPath equality contract.
    [Fact]
    public void ToFileInfo_roundtrip_preserves_SPath_equality()
    {
        var original = SPath.From(Path.Combine(_tempRoot, "roundtrip.txt"));

        var info = original.ToFileInfo();
        var reconstructed = SPath.From(info.FullName);

        reconstructed.ShouldBe(original);
    }

    [Fact]
    public void ToFileInfo_throws_ArgumentNullException_on_null_path()
    {
        SPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.ToFileInfo());
        ex.ParamName.ShouldBe("path");
    }

#endregion

#region ToDirectoryInfo

    [Fact]
    public void ToDirectoryInfo_returns_DirectoryInfo_with_matching_FullName()
    {
        var dirPath = Path.Combine(_tempRoot, "existing-dir");
        Directory.CreateDirectory(dirPath);

        var path = SPath.From(dirPath);

        var info = path.ToDirectoryInfo();

        info.ShouldNotBeNull();
        // DirectoryInfo.FullName may or may not include a trailing separator depending on
        // platform and the constructor input. We compare against SPath.Value directly:
        // SPath normalization is already settled, and the test validates FullName *matches*
        // that canonical form.
        info.FullName.ShouldBe(path.Value);
    }

    [Fact]
    public void ToDirectoryInfo_does_not_require_directory_to_exist()
    {
        var missingDir = Path.Combine(_tempRoot, "not-there");
        var path = SPath.From(missingDir);

        var info = Should.NotThrow(() => path.ToDirectoryInfo());

        info.Exists.ShouldBeFalse();
        info.FullName.ShouldBe(path.Value);
    }

    [Fact]
    public void ToDirectoryInfo_roundtrip_preserves_SPath_equality()
    {
        var original = SPath.From(Path.Combine(_tempRoot, "roundtrip-dir"));

        var info = original.ToDirectoryInfo();
        var reconstructed = SPath.From(info.FullName);

        reconstructed.ShouldBe(original);
    }

    [Fact]
    public void ToDirectoryInfo_throws_ArgumentNullException_on_null_path()
    {
        SPath? p = null;

        var ex = Should.Throw<ArgumentNullException>(() => p!.ToDirectoryInfo());
        ex.ParamName.ShouldBe("path");
    }

#endregion
}
