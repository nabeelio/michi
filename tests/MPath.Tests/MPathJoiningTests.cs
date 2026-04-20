using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: CORE-04 — path joining via / operator and Join(params)
public class MPathJoiningTests {
    // CORE-04: basic join via /
    [Fact]
    public void SlashOperator_BasicJoin()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/foo") / "bar").ToUnixString().ShouldBe("/foo/bar");
    }

    // CORE-04 + D-16a: leading / on RHS is STRIPPED (RHS-always-relative rule)
    [Fact]
    public void SlashOperator_LeadingSlashOnRhs_IsStripped()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/var/www") / "/etc").ToUnixString().ShouldBe("/var/www/etc");
    }

    // CORE-04 + D-16a: leading backslash on RHS is ALSO stripped
    [Fact]
    public void SlashOperator_LeadingBackslashOnRhs_IsStripped()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/var") / "\\etc").ToUnixString().ShouldBe("/var/etc");
    }

    // CORE-04: RHS may contain internal separators (joined + normalized)
    [Fact]
    public void SlashOperator_RhsWithInternalSeparators_JoinsAndNormalizes()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/a") / "b/c/d").ToUnixString().ShouldBe("/a/b/c/d");
    }

    // CORE-04 + D-18: RHS may contain `..` (join does NOT prevent traversal)
    [Fact]
    public void SlashOperator_RhsWithDoubleDot_Traverses()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/a/b") / "../c").ToUnixString().ShouldBe("/a/c");
    }

    // CORE-04: null segment throws ArgumentNullException
    [Fact]
    public void SlashOperator_NullSegment_ThrowsArgumentNullException()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<ArgumentNullException>(() => {
                var _ = p / null!;
            }
        );

        ex.ParamName.ShouldBe("segment");
        ex.Message.ShouldContain("empty string");
    }

    // CORE-04: empty segment returns same path unchanged
    [Fact]
    public void SlashOperator_EmptySegment_ReturnsUnchanged()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo");
        (p / "").ShouldBe(p);
    }

    // CORE-04: Join(params) chains segments
    [Fact]
    public void Join_ChainsSegments()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a").Join("b", "c", "d").ToUnixString().ShouldBe("/a/b/c/d");
    }

    // CORE-04: Join(params) skips null/empty segments
    [Fact]
    public void Join_SkipsNullAndEmpty()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a")
               .Join(
                    null!,
                    "",
                    "b",
                    null!,
                    "c"
                )
               .ToUnixString()
               .ShouldBe("/a/b/c");
    }

    // CORE-04: Join(null or empty array) returns self
    [Fact]
    public void Join_NullOrEmpty_ReturnsSelf()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a");
        p.Join().ShouldBe(p);
        p.Join(null!).ShouldBe(p);
    }

    // Phase success criterion 2: (From("/a") / "b" / "c").Segments == ["a","b","c"] and equals From("/a/b/c")
    [Fact]
    public void Joining_ProducesSegmentsAndEquality_Phase1Criterion2()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var joined = MPath.From("/a") / "b" / "c";
        string[] expected = [
            "a",
            "b",
            "c",
        ];

        joined.Segments.ShouldBe(expected);
        joined.ShouldBe(MPath.From("/a/b/c"));
    }
}
