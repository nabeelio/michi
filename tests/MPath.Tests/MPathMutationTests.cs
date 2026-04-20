using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: MUT-01 — WithName / WithExtension / WithoutExtension
public class MPathMutationTests {
    // MUT-01 + D-28: WithName replaces the final segment
    [Fact]
    public void WithName_ReplacesFinalSegment()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/b/c.txt").WithName("d.md").ToUnixString().ShouldBe("/a/b/d.md");
    }

    // MUT-01 + D-29b: WithName containing `/` throws with verbose message
    [Fact]
    public void WithName_WithForwardSlash_Throws_WithD29bMessage()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo\bar" : "/foo/bar");
        var ex = Should.Throw<InvalidPathException>(() => p.WithName("sub/name"));
        ex.Message.ShouldContain("contains a directory separator");
        ex.Message.ShouldContain("Use the / operator or Join()");
    }

    // MUT-01 + D-29b: WithName containing `\` also throws
    [Fact]
    public void WithName_WithBackslash_Throws()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo\bar" : "/foo/bar");
        var ex = Should.Throw<InvalidPathException>(() => p.WithName("sub\\name"));
        ex.Message.ShouldContain("contains a directory separator");
    }

    // MUT-01 + D-28: WithName null throws ArgumentNullException with verbose message
    [Fact]
    public void WithName_Null_ThrowsArgumentNullException()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<ArgumentNullException>(() => p.WithName(null!));
        ex.ParamName.ShouldBe("name");
        ex.Message.ShouldContain("non-empty segment string");
    }

    // MUT-01 + D-29: WithExtension accepts leading-dot or no-dot form
    [Fact]
    public void WithExtension_AcceptsBothDotAndNoDot()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo").WithExtension("txt").ToUnixString().ShouldBe("/a/foo.txt");
        MPath.From("/a/foo").WithExtension(".txt").ToUnixString().ShouldBe("/a/foo.txt");
    }

    // MUT-01 + D-29: WithExtension replaces existing extension
    [Fact]
    public void WithExtension_ReplacesExistingExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo.txt").WithExtension("md").ToUnixString().ShouldBe("/a/foo.md");
    }

    // MUT-01 + D-29: WithExtension(null) equivalent to WithoutExtension
    [Fact]
    public void WithExtension_Null_EquivalentToWithoutExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/foo.txt");
        p.WithExtension(null).ShouldBe(p.WithoutExtension());
        p.WithExtension(null).ToUnixString().ShouldBe("/a/foo");
    }

    // MUT-01 + D-29: WithExtension("") equivalent to WithoutExtension
    [Fact]
    public void WithExtension_Empty_EquivalentToWithoutExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo.txt").WithExtension("").ToUnixString().ShouldBe("/a/foo");
    }

    // MUT-01 + D-29a: WithExtension(".") throws InvalidPathException with the exact D-29a message
    [Fact]
    public void WithExtension_BareDot_Throws_WithD29aMessage()
    {
        var p = MPath.From(PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo");
        var ex = Should.Throw<InvalidPathException>(() => p.WithExtension("."));
        ex.AttemptedPath.ShouldBe(".");
        ex.Message.ShouldContain("Extension '.' is not valid");
        ex.Message.ShouldContain("Use WithoutExtension() to remove");
    }

    // MUT-01 + D-28: WithoutExtension strips extension
    [Fact]
    public void WithoutExtension_StripsExtension()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/a/foo.txt").WithoutExtension().ToUnixString().ShouldBe("/a/foo");
    }

    // MUT-01 + D-28: WithoutExtension on extension-less path is idempotent
    [Fact]
    public void WithoutExtension_NoExtension_IsIdempotent()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = MPath.From("/a/foo");
        p.WithoutExtension().ShouldBe(p);
    }

    // MUT-01 + D-28: Mutation produces NEW instances; original is unchanged
    [Fact]
    public void Mutation_ProducesNewInstance_OriginalUnchanged()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var original = MPath.From("/a/foo.txt");
        var mutated = original.WithName("bar.md");
        ReferenceEquals(original, mutated).ShouldBeFalse();
        original.ToUnixString().ShouldBe("/a/foo.txt");
        mutated.ToUnixString().ShouldBe("/a/bar.md");
    }
}
