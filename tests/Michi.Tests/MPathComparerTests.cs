using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public class MPathComparerTests {
    // MPathComparer.Ordinal is case-sensitive regardless of host OS.
    [Fact]
    public void OrdinalComparer_IsCaseSensitive_OnWindowsAndMacOS()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var a = PlatformTestHelpers.IsWindows ? MPath.From(@"C:\Foo") : MPath.From("/Foo");
        var b = PlatformTestHelpers.IsWindows ? MPath.From(@"C:\foo") : MPath.From("/foo");
        MPathComparer.Ordinal.Equals(a, b).ShouldBeFalse();
        MPathComparer.Ordinal.GetHashCode(a).ShouldNotBe(MPathComparer.Ordinal.GetHashCode(b));
    }

    // MPathComparer.OrdinalIgnoreCase is case-insensitive regardless of host OS.
    [Fact]
    public void OrdinalIgnoreCaseComparer_IsCaseInsensitive_OnLinux()
    {
        if (!PlatformTestHelpers.IsLinux)
            return;

        var a = MPath.From("/Foo");
        var b = MPath.From("/foo");
        MPathComparer.OrdinalIgnoreCase.Equals(a, b).ShouldBeTrue();
        MPathComparer.OrdinalIgnoreCase.GetHashCode(a).ShouldBe(MPathComparer.OrdinalIgnoreCase.GetHashCode(b));
    }

    // HashSet with explicit comparer overrides the host-OS default.
    [Fact]
    public void HashSet_WithOrdinalComparer_DoesNotDedupCaseVariantsEvenOnWindows()
    {
        if (PlatformTestHelpers.IsLinux)
            return;

        var inputA = PlatformTestHelpers.IsWindows ? @"C:\Foo" : "/Foo";
        var inputB = PlatformTestHelpers.IsWindows ? @"C:\foo" : "/foo";
        var set = new HashSet<MPath>(MPathComparer.Ordinal) {
            MPath.From(inputA),
            MPath.From(inputB),
        };

        set.Count.ShouldBe(2); // both retained -- ordinal is case-sensitive
    }

    // Comparer throws a verbose ArgumentNullException on null GetHashCode arg.
    [Fact]
    public void OrdinalComparer_GetHashCode_NullArg_ThrowsWithVerboseMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPathComparer.Ordinal.GetHashCode(null!));
        ex.Message.ShouldContain("non-null instance");
    }
}
