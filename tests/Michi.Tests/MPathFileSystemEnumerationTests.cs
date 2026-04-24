using Michi.FileSystem;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// Tests cover the two lazy directory-walking extension methods: EnumerateFiles and
// EnumerateDirectories. The core contracts being locked down here are:
//
//   1. The returned sequence is streaming (D-07). A consumer that takes only the first
//      element must not trigger enumeration of the rest.
//   2. Elements are MPath instances produced via MPath.From, not raw strings.
//   3. Null-argument checks fire EAGERLY -- before any iteration happens -- so the
//      classic "yield-return swallows validation" footgun does not apply.
//   4. Ordering is never asserted (D-10) -- the tests use set comparisons.
public sealed class MPathFileSystemEnumerationTests : IDisposable {
    private readonly string _tempRoot;

    public MPathFileSystemEnumerationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"michi-fs-enum-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, true);
        }
    }

#region EnumerateFiles

    [Fact]
    public void EnumerateFiles_returns_matching_files_top_only()
    {
        var root = MPath.From(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "a");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "sub"));
        File.WriteAllText(Path.Combine(_tempRoot, "sub", "b.txt"), "b");

        var results = root.EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly).ToList();

        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("a.txt");
    }

    [Fact]
    public void EnumerateFiles_returns_all_matching_files_all_directories()
    {
        var root = MPath.From(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "a");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "sub"));
        File.WriteAllText(Path.Combine(_tempRoot, "sub", "b.txt"), "b");

        var names = root
               .EnumerateFiles("*.txt", SearchOption.AllDirectories)
               .Select(p => p.Name)
               .ToHashSet();

        names.Count.ShouldBe(2);
        names.ShouldContain("a.txt");
        names.ShouldContain("b.txt");
    }

    // Pattern filtering is delegated to Directory.EnumerateFiles; verify we don't
    // accidentally drop the pattern on the floor.
    [Fact]
    public void EnumerateFiles_pattern_star_dot_ext_filters_correctly()
    {
        var root = MPath.From(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "keep.txt"), "a");
        File.WriteAllText(Path.Combine(_tempRoot, "skip.log"), "b");

        var names = root
               .EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly)
               .Select(p => p.Name)
               .ToHashSet();

        names.ShouldContain("keep.txt");
        names.ShouldNotContain("skip.log");
    }

    // Elements must be MPath instances, not raw strings. We verify by calling an
    // MPath-only method on each element.
    [Fact]
    public void EnumerateFiles_returns_MPath_instances()
    {
        var root = MPath.From(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "a");

        var results = root.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToList();

        results.Count.ShouldBe(1);
        results[0].ShouldNotBeNull();
        results[0].FileExists().ShouldBeTrue();
    }

    // Laziness contract: wrap the enumeration in Select with a side-effect counter.
    // Taking 1 element must consume exactly 1 BCL element, not the whole tree.
    [Fact]
    public void EnumerateFiles_is_lazy()
    {
        var root = MPath.From(_tempRoot);
        for (var i = 0; i < 10; i++) {
            File.WriteAllText(Path.Combine(_tempRoot, $"f{i}.txt"), "");
        }

        var counter = 0;
        var _ = root
               .EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly)
               .Select(p => {
                        counter++;

                        return p;
                    }
                )
               .Take(1)
               .ToList();

        counter.ShouldBe(1);
    }

    // The yield-return footgun: arg validation MUST happen in the public method, not
    // inside the iterator. Otherwise a caller that never iterates (var q = ...;
    // without foreach) silently succeeds with a null receiver.
    [Fact]
    public void EnumerateFiles_throws_eagerly_on_null_path()
    {
        Should.Throw<ArgumentNullException>(() =>
                MPathExtensions.EnumerateFiles(null!, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Fact]
    public void EnumerateFiles_throws_eagerly_on_null_pattern()
    {
        var root = MPath.From(_tempRoot);

        Should.Throw<ArgumentNullException>(() => root.EnumerateFiles(null!, SearchOption.TopDirectoryOnly));
    }

    // Missing-path failure is BCL-driven and happens on first MoveNext (lazy). Document
    // that the public method does NOT pre-probe existence.
    [Fact]
    public void EnumerateFiles_throws_DirectoryNotFoundException_on_missing_path_at_iteration_time()
    {
        var missing = MPath.From(Path.Combine(_tempRoot, "does-not-exist"));

        // Constructing the query must not throw -- only iterating does.
        var q = missing.EnumerateFiles("*", SearchOption.TopDirectoryOnly);
        Should.NotThrow(() => q.ToString());

        Should.Throw<DirectoryNotFoundException>(() => q.ToList());
    }

    // Ordering is explicitly not guaranteed (D-10). Test asserts only set membership.
    [Fact]
    public void EnumerateFiles_no_ordering_guarantee()
    {
        var root = MPath.From(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "c.txt"), "");
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "");
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), "");

        var names = root
               .EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly)
               .Select(p => p.Name)
               .ToHashSet();

        names.Count.ShouldBe(3);
        names.ShouldContain("a.txt");
        names.ShouldContain("b.txt");
        names.ShouldContain("c.txt");
    }

#endregion

#region EnumerateDirectories

    [Fact]
    public void EnumerateDirectories_returns_matching_directories_top_only()
    {
        var root = MPath.From(_tempRoot);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "d1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "d2"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "d1", "nested"));

        var names = root
               .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
               .Select(p => p.Name)
               .ToHashSet();

        names.Count.ShouldBe(2);
        names.ShouldContain("d1");
        names.ShouldContain("d2");
        names.ShouldNotContain("nested");
    }

    [Fact]
    public void EnumerateDirectories_returns_all_matching_directories_all_directories()
    {
        var root = MPath.From(_tempRoot);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "d1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "d1", "nested"));

        var names = root
               .EnumerateDirectories("*", SearchOption.AllDirectories)
               .Select(p => p.Name)
               .ToHashSet();

        names.Count.ShouldBe(2);
        names.ShouldContain("d1");
        names.ShouldContain("nested");
    }

    [Fact]
    public void EnumerateDirectories_returns_MPath_instances()
    {
        var root = MPath.From(_tempRoot);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "child"));

        var results = root.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).ToList();

        results.Count.ShouldBe(1);
        results[0].ShouldNotBeNull();
        results[0].DirectoryExists().ShouldBeTrue();
    }

    [Fact]
    public void EnumerateDirectories_is_lazy()
    {
        var root = MPath.From(_tempRoot);
        for (var i = 0; i < 10; i++) {
            Directory.CreateDirectory(Path.Combine(_tempRoot, $"d{i}"));
        }

        var counter = 0;
        var _ = root
               .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
               .Select(p => {
                        counter++;

                        return p;
                    }
                )
               .Take(1)
               .ToList();

        counter.ShouldBe(1);
    }

    [Fact]
    public void EnumerateDirectories_throws_eagerly_on_null_path()
    {
        Should.Throw<ArgumentNullException>(() => MPathExtensions.EnumerateDirectories(
                    null!,
                    "*",
                    SearchOption.TopDirectoryOnly
                )
        );
    }

    [Fact]
    public void EnumerateDirectories_throws_eagerly_on_null_pattern()
    {
        var root = MPath.From(_tempRoot);

        Should.Throw<ArgumentNullException>(() => root.EnumerateDirectories(null!, SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void
            EnumerateDirectories_throws_DirectoryNotFoundException_on_missing_path_at_iteration_time()
    {
        var missing = MPath.From(Path.Combine(_tempRoot, "does-not-exist"));

        var q = missing.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

        Should.Throw<DirectoryNotFoundException>(() => q.ToList());
    }

#endregion
}
