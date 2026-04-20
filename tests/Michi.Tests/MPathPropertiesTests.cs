using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: CORE-03 — derived properties (Name/Extension/NameWithoutExtension/HasExtension/Segments/Depth/Root)
public class MPathPropertiesTests {
    // CORE-03: Name returns the final segment
    [Fact]
    public void Name_ReturnsFinalSegment()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/foo/bar/baz.txt").Name.ShouldBe("baz.txt");
        MPath.From("/foo").Name.ShouldBe("foo");
    }

    // CORE-03: Extension includes the leading dot
    [Fact]
    public void Extension_IncludesLeadingDot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b.txt").Extension.ShouldBe(".txt");
        MPath.From("/a/b").Extension.ShouldBe("");
    }

    // CORE-03: NameWithoutExtension strips the trailing extension
    [Fact]
    public void NameWithoutExtension_StripsTrailingExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo.txt").NameWithoutExtension.ShouldBe("foo");
        MPath.From("/a/foo").NameWithoutExtension.ShouldBe("foo");
    }

    // CORE-03: hidden-file dot-prefix is NOT treated as an extension separator
    [Fact]
    public void HiddenFile_LeadingDot_NotTreatedAsExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/home/user/.bashrc");
        p.Name.ShouldBe(".bashrc");
        p.Extension.ShouldBe("");
        p.NameWithoutExtension.ShouldBe(".bashrc");
        p.HasExtension.ShouldBeFalse();
    }

    // CORE-03: Segments returns the path below the root
    [Fact]
    public void Segments_ReturnsSegmentsBelowRoot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var segs = MPath.From("/a/b/c").Segments;
        string[] expected = [
            "a",
            "b",
            "c",,
        ];

        segs.ShouldBe(expected);
    }

    // PITFALLS M-17: Segments returns a defensive copy (mutating the returned array doesn't affect the instance)
    [Fact]
    public void Segments_ReturnsDefensiveCopy()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/b/c");
        var first = p.Segments;
        first[0] = "MUTATED";
        var second = p.Segments;
        second[0].ShouldBe("a"); // internal state unaffected
    }

    // CORE-03: Depth matches segment count
    [Fact]
    public void Depth_MatchesSegmentCount()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/").Depth.ShouldBe(0);
        MPath.From("/a").Depth.ShouldBe(1);
        MPath.From("/a/b/c").Depth.ShouldBe(3);
    }

    // CORE-03: HasExtension matches Extension.Length > 0
    [Theory]
    [InlineData("/foo/bar.txt", true)]
    [InlineData("/foo/bar", false)]
    [InlineData("/foo/.hidden", false)]
    public void HasExtension_MatchesExtensionPresence(string path, bool expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From(path).HasExtension.ShouldBe(expected);
    }

    // CORE-03: Root is "/" on Unix absolute paths
    [Fact]
    public void Root_IsForwardSlash_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c").Root.ShouldBe("/");
    }

    // CORE-03: Root includes drive letter + trailing slash on Windows
    [Fact]
    public void Root_IncludesDriveLetter_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From(@"C:\foo\bar");
        p.Root.ShouldBe("C:/");
    }
}
