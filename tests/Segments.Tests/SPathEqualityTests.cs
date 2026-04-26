using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathEqualityTests {
    // Host-OS case-insensitive equality on Windows/macOS.
    [Fact]
    public void Equals_DifferentCase_TrueOnWindowsAndMacOS()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var baseInput = PlatformTestHelpers.IsWindows ? @"C:\Foo\Bar" : "/Foo/Bar";
        var otherInput = PlatformTestHelpers.IsWindows ? @"c:\foo\bar" : "/foo/bar";
        SPath.From(baseInput).Equals(SPath.From(otherInput)).ShouldBeTrue();
    }

    // Host-OS case-sensitive equality on Linux.
    [Fact]
    public void Equals_DifferentCase_FalseOnLinux()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        SPath.From("/Foo/Bar").Equals(SPath.From("/foo/bar")).ShouldBeFalse();
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
        var pa = SPath.From(a);
        var pb = SPath.From(b);
        if (pa.Equals(pb)) {
            pa.GetHashCode()
                   .ShouldBe(
                        pb.GetHashCode(),
                        $"Equal SPaths must have equal hashes. '{a}' vs '{b}'."
                    );
        }
    }

    // Equals(object) delegates to Equals(SPath).
    [Fact]
    public void EqualsObject_DelegatesToEqualsSPath()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        object otherSPath = SPath.From("/foo");
        object notAnSPath = "not an mpath";
        SPath.From("/foo").Equals(otherSPath).ShouldBeTrue();
        SPath.From("/foo").Equals(notAnSPath).ShouldBeFalse();
        SPath.From("/foo").Equals((object?) null).ShouldBeFalse();
    }

    // == and != operators.
    [Fact]
    public void OperatorEquality_HandlesNullsAndReferenceEquality()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var a = SPath.From("/foo");
        var b = SPath.From("/foo");
        SPath? c = null;
        SPath? d = null;

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

        var a = SPath.From("/a");
        var b = SPath.From("/b");
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

        var set = new HashSet<SPath> {
            SPath.From("/Foo/Bar"),
            SPath.From("/foo/bar"),
            SPath.From("/FOO/BAR"),
        };

        set.Count.ShouldBe(1);
    }
}
