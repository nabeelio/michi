using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathEqualityTests {
    // Host-OS case-insensitive equality on Windows/macOS.
    [Fact]
    public void Equals_DifferentCase_TrueOnWindowsAndMacOS()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var baseInput = PlatformTestHelpers.IsWindows ? @"C:\Foo\Bar" : "/Foo/Bar";
        var otherInput = PlatformTestHelpers.IsWindows ? @"c:\foo\bar" : "/foo/bar";
        MPath.From(baseInput).Equals(MPath.From(otherInput)).ShouldBeTrue();
    }

    // Host-OS case-sensitive equality on Linux.
    [Fact]
    public void Equals_DifferentCase_FalseOnLinux()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        MPath.From("/Foo/Bar").Equals(MPath.From("/foo/bar")).ShouldBeFalse();
    }

    // Equals/GetHashCode contract -- equal paths must produce equal hashes (CA1065).
    // Case variants exercise the case-insensitive branch on Windows/macOS; on Linux
    // the assertion is vacuously true (values differ, implication holds).
    [Theory]
    [InlineData("/Foo/Bar", "/FOO/BAR")]
    [InlineData("/foo/Bar", "/foo/bar")]
    [InlineData("/FOO", "/foo")]
    [InlineData("/a/B/c", "/A/b/C")]
    [InlineData("/alpha/beta", "/ALPHA/BETA")]
    [InlineData("/MixedCase/Path", "/mixedcase/path")]
    public void HashCode_IsConsistentWithEquals_ForCaseVariants(string a, string b)
    {
        var pa = MPath.From(a);
        var pb = MPath.From(b);
        if (pa.Equals(pb)) {
            pa.GetHashCode()
                   .ShouldBe(
                        pb.GetHashCode(),
                        $"Equal MPaths must have equal hashes. '{a}' vs '{b}'."
                    );
        }
    }

    // Equals(object) delegates to Equals(MPath).
    [Fact]
    public void EqualsObject_DelegatesToEqualsMPath()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        object otherMPath = MPath.From("/foo");
        object notAnMPath = "not an mpath";
        MPath.From("/foo").Equals(otherMPath).ShouldBeTrue();
        MPath.From("/foo").Equals(notAnMPath).ShouldBeFalse();
        MPath.From("/foo").Equals((object?) null).ShouldBeFalse();
    }

    // == and != operators.
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

    // CompareTo produces a total order consistent with Equals.
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

    // HashSet dedup uses equality correctly on the host OS.
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
