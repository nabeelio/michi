using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathPropertiesTests {
    // Name returns the final segment.
    [Fact]
    public void Name_ReturnsFinalSegment()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/foo/bar/baz.txt").Name.ShouldBe("baz.txt");
        SPath.From("/foo").Name.ShouldBe("foo");
    }

    // Extension includes the leading dot.
    [Fact]
    public void Extension_IncludesLeadingDot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/b.txt").Extension.ShouldBe(".txt");
        SPath.From("/a/b").Extension.ShouldBe("");
    }

    // NameWithoutExtension strips the trailing extension.
    [Fact]
    public void NameWithoutExtension_StripsTrailingExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/foo.txt").NameWithoutExtension.ShouldBe("foo");
        SPath.From("/a/foo").NameWithoutExtension.ShouldBe("foo");
    }

    // Hidden-file leading dot is NOT treated as an extension separator.
    [Fact]
    public void HiddenFile_LeadingDot_NotTreatedAsExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/home/user/.bashrc");
        p.Name.ShouldBe(".bashrc");
        p.Extension.ShouldBe("");
        p.NameWithoutExtension.ShouldBe(".bashrc");
        p.HasExtension.ShouldBeFalse();
    }

    // Segments returns the path below the root.
    [Fact]
    public void Segments_ReturnsSegmentsBelowRoot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var segs = SPath.From("/a/b/c").Segments;
        string[] expected = [
            "a",
            "b",
            "c",
        ];

        segs.ShouldBe(expected);
    }

    // Segments is a read-only view -- the returned collection cannot mutate internal state.
    [Fact]
    public void Segments_IsReadOnly()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From("/a/b/c");
        p.Segments.ShouldBeAssignableTo<IReadOnlyList<string>>();
        p.Segments[0].ShouldBe("a");
        p.Segments.Count.ShouldBe(3);
    }

    // Depth matches segment count.
    [Fact]
    public void Depth_MatchesSegmentCount()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/").Depth.ShouldBe(0);
        SPath.From("/a").Depth.ShouldBe(1);
        SPath.From("/a/b/c").Depth.ShouldBe(3);
    }

    // HasExtension matches Extension.Length > 0.
    [Theory]
    [InlineData("/foo/bar.txt", true)]
    [InlineData("/foo/bar", false)]
    [InlineData("/foo/.hidden", false)]
    public void HasExtension_MatchesExtensionPresence(string path, bool expected)
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From(path).HasExtension.ShouldBe(expected);
    }

    // Root is "/" on Unix absolute paths.
    [Fact]
    public void Root_IsForwardSlash_OnUnix()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        SPath.From("/a/b/c").Root.ShouldBe("/");
    }

    // Root includes drive letter + trailing slash on Windows.
    [Fact]
    public void Root_IncludesDriveLetter_OnWindows()
    {
        if (!PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.From(@"C:\foo\bar");
        p.Root.ShouldBe("C:/");
    }
}
