using Shouldly;
using Xunit;

namespace Michi.Tests;

// Trivial "Home matches UserProfile" / "Temp matches GetTempPath" tests are omitted --
// they duplicate the constructors. What remains pins the two behavioral contracts that
// actually matter: Home/Temp are cached singletons; CurrentDirectory is NOT cached.
public class WellKnownPathsTests {
    // Home and Temp are lazy singletons (same instance on repeat access).
    [Fact]
    public void HomeAndTemp_AreLazySingletons()
    {
        var homeA = MPath.Home;
        var homeB = MPath.Home;
        ReferenceEquals(homeA, homeB).ShouldBeTrue();

        var tempA = MPath.Temp;
        var tempB = MPath.Temp;
        ReferenceEquals(tempA, tempB).ShouldBeTrue();
    }

    // CurrentDirectory is evaluated on every access, NOT cached -- guards against the
    // stale-CWD bug pattern where a cached value silently goes out of date after a
    // Directory.SetCurrentDirectory call.
    [Fact]
    public void CurrentDirectory_IsReevaluatedOnEachAccess()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try {
            var first = MPath.CurrentDirectory;
            Directory.SetCurrentDirectory(Path.GetTempPath());
            var second = MPath.CurrentDirectory;
            first.Equals(second).ShouldBeFalse();
        }
        finally {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}
