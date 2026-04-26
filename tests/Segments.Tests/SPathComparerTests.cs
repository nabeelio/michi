using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathComparerTests {
    // SPathComparer.Ordinal is case-sensitive regardless of host OS.
    [Fact]
    public void OrdinalComparer_IsCaseSensitive_OnWindowsAndMacOS()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var a = PlatformTestHelpers.IsWindows ? SPath.From(@"C:\Foo") : SPath.From("/Foo");
        var b = PlatformTestHelpers.IsWindows ? SPath.From(@"C:\foo") : SPath.From("/foo");
        SPathComparer.Ordinal.Equals(a, b).ShouldBeFalse();
        SPathComparer.Ordinal.GetHashCode(a).ShouldNotBe(SPathComparer.Ordinal.GetHashCode(b));
    }

    // SPathComparer.OrdinalIgnoreCase is case-insensitive regardless of host OS.
    [Fact]
    public void OrdinalIgnoreCaseComparer_IsCaseInsensitive_OnLinux()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        var a = SPath.From("/Foo");
        var b = SPath.From("/foo");
        SPathComparer.OrdinalIgnoreCase.Equals(a, b).ShouldBeTrue();
        SPathComparer.OrdinalIgnoreCase.GetHashCode(a).ShouldBe(SPathComparer.OrdinalIgnoreCase.GetHashCode(b));
    }

    // HashSet with explicit comparer overrides the host-OS default.
    [Fact]
    public void HashSet_WithOrdinalComparer_DoesNotDedupCaseVariantsEvenOnWindows()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var inputA = PlatformTestHelpers.IsWindows ? @"C:\Foo" : "/Foo";
        var inputB = PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo";
        var set = new HashSet<SPath>(SPathComparer.Ordinal) {
            SPath.From(inputA),
            SPath.From(inputB),
        };

        set.Count.ShouldBe(2); // both retained -- ordinal is case-sensitive
    }

    // Comparer throws a verbose ArgumentNullException on null GetHashCode arg.
    [Fact]
    public void OrdinalComparer_GetHashCode_NullArg_ThrowsWithVerboseMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => SPathComparer.Ordinal.GetHashCode(null!));
        ex.Message.ShouldContain("non-null instance");
    }
}
