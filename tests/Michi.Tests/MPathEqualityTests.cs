using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: CORE-06 — equality, hash, compare, ==/!= operators
public class MPathEqualityTests {
    // CORE-06: host-OS case-insensitive equality on Windows/macOS
    [Fact]
    public void Equals_DifferentCase_TrueOnWindowsAndMacOS()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var baseInput = PlatformTestHelpers.IsWindows ? @"C:\Foo\Bar" : "/Foo/Bar";
        var otherInput = PlatformTestHelpers.IsWindows ? @"c:\foo\bar" : "/foo/bar";
        MPath.From(baseInput).Equals(MPath.From(otherInput)).ShouldBeTrue();
    }

    // CORE-06: host-OS case-sensitive equality on Linux
    [Fact]
    public void Equals_DifferentCase_FalseOnLinux()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        MPath.From("/Foo/Bar").Equals(MPath.From("/foo/bar")).ShouldBeFalse();
    }

    // D-43 + PITFALLS C-01: Nuke-bug regression guard — case variants that compare equal MUST have matching hash codes.
    // This test exists specifically to prevent the hash/equality inconsistency bug that Nuke's AbsolutePath shipped with.
    [Theory]
    [InlineData("/Foo/Bar", "/FOO/BAR")]
    [InlineData("/foo/Bar", "/foo/bar")]
    [InlineData("/FOO", "/foo")]
    [InlineData("/a/B/c", "/A/b/C")]
    [InlineData("/alpha/beta", "/ALPHA/BETA")]
    [InlineData("/MixedCase/Path", "/mixedcase/path")]
    public void HashCode_IsConsistentWithEquals_ForCaseVariants_NukeRegressionGuard(string a, string b)
    {
        // On Linux (case-sensitive): equals and hashes both differ — test still passes because the contract
        //   is "if a.Equals(b) then hash(a) == hash(b)", which is vacuously true when Equals is false.
        // On Windows/macOS (case-insensitive): a.Equals(b) is true AND hashes must match.
        var pa = MPath.From(a);
        var pb = MPath.From(b);
        if (pa.Equals(pb)) {
            pa.GetHashCode()
                   .ShouldBe(
                        pb.GetHashCode(),
                        $"Equal MPaths must have equal hashes. '{a}' vs '{b}'. This is the Nuke-AbsolutePath bug regression guard (D-43)."
                    );
        }
    }

    // CORE-06: identical paths compare equal regardless of input form (backslash vs forward slash)
    [Fact]
    public void Equals_BackslashAndForwardSlashInput_CompareEqual()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        MPath.From("/foo/bar").Equals(MPath.From("/foo\\bar")).ShouldBeTrue();
    }

    // CORE-06: Equals(object) delegates to Equals(MPath)
    [Fact]
    public void EqualsObject_DelegatesToEqualsMPath()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        object other = MPath.From("/foo");
        MPath.From("/foo").Equals(other).ShouldBeTrue();
        MPath.From("/foo").Equals("not an mpath").ShouldBeFalse();
        MPath.From("/foo").Equals((object?) null).ShouldBeFalse();
    }

    // CORE-06: == and != operators
    [Fact]
    public void OperatorEquality_HandlesNullsAndReferenceEquality()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var a = MPath.From("/foo");
        var b = MPath.From("/foo");
        MPath? c = null;
        MPath? d = null;

        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
        (a == c).ShouldBeFalse();
        (c == d).ShouldBeTrue();
        (c != d).ShouldBeFalse();
    }

    // CORE-06: CompareTo produces a total order consistent with Equals
    [Fact]
    public void CompareTo_ProducesTotalOrderConsistentWithEquals()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var a = MPath.From("/a");
        var b = MPath.From("/b");
        a.CompareTo(a).ShouldBe(0);
        a.CompareTo(b).ShouldBeLessThan(0);
        b.CompareTo(a).ShouldBeGreaterThan(0);
        a.CompareTo(null).ShouldBeGreaterThan(0); // null precedes any value -> this is > 0
    }

    // CORE-06: HashSet dedup uses equality correctly on the host OS
    [Fact]
    public void HashSet_DedupsOnHostOsCase()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var set = new HashSet<MPath> {
            MPath.From("/Foo/Bar"),
            MPath.From("/foo/bar"),
            MPath.From("/FOO/BAR"),
        };

        set.Count.ShouldBe(1);
    }
}
