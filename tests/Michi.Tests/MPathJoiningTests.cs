using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathJoiningTests {
    // Basic join via the / operator.
    [Fact]
    public void SlashOperator_BasicJoin()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/foo") / "bar").ToUnixString().ShouldBe("/foo/bar");
    }

    // Leading / on the RHS is stripped -- the RHS is always treated as relative.
    [Fact]
    public void SlashOperator_LeadingSlashOnRhs_IsStripped()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/var/www") / "/etc").ToUnixString().ShouldBe("/var/www/etc");
    }

    // Leading backslash on the RHS is also stripped.
    [Fact]
    public void SlashOperator_LeadingBackslashOnRhs_IsStripped()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/var") / "\\etc").ToUnixString().ShouldBe("/var/etc");
    }

    // The RHS may contain internal separators (joined + normalized).
    [Fact]
    public void SlashOperator_RhsWithInternalSeparators_JoinsAndNormalizes()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/a") / "b/c/d").ToUnixString().ShouldBe("/a/b/c/d");
    }

    // The RHS may contain `..` -- join does NOT prevent traversal. Validate the result if needed.
    [Fact]
    public void SlashOperator_RhsWithDoubleDot_Traverses()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        (MPath.From("/a/b") / "../c").ToUnixString().ShouldBe("/a/c");
    }

    // Null segment throws ArgumentNullException.
    [Fact]
    public void SlashOperator_NullSegment_ThrowsArgumentNullException()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<ArgumentNullException>(() => { _ = p / null!; });

        ex.ParamName.ShouldBe("segment");
        ex.Message.ShouldContain("empty string");
    }

    // Empty segment returns the same path unchanged.
    [Fact]
    public void SlashOperator_EmptySegment_ReturnsUnchanged()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/foo");
        (p / "").ShouldBe(p);
    }

    // Join(params) chains segments.
    [Fact]
    public void Join_ChainsSegments()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a").Join("b", "c", "d").ToUnixString().ShouldBe("/a/b/c/d");
    }

    // Chained join produces the expected segment list and equality with the direct form.
    [Fact]
    public void Joining_ProducesSegmentsAndEquality()
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
