using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: WELL-01 — Home / Temp / CurrentDirectory well-known paths
public class WellKnownPathsTests {
    // WELL-01 + D-30: Home returns the same instance on repeat access (lazy singleton)
    [Fact]
    public void Home_ReturnsSameInstanceOnRepeatAccess()
    {
        var a = MPath.Home;
        var b = MPath.Home;
        ReferenceEquals(a, b).ShouldBeTrue();
    }

    // WELL-01 + D-31: Temp returns the same instance on repeat access (lazy singleton)
    [Fact]
    public void Temp_ReturnsSameInstanceOnRepeatAccess()
    {
        var a = MPath.Temp;
        var b = MPath.Temp;
        ReferenceEquals(a, b).ShouldBeTrue();
    }

    // WELL-01 + D-32 + PITFALLS m-23: CurrentDirectory is evaluated on every access, NOT cached
    [Fact]
    public void CurrentDirectory_IsReevaluatedOnEachAccess()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        try {
            var first = MPath.CurrentDirectory;
            var newCwd = Path.GetTempPath();
            Directory.SetCurrentDirectory(newCwd);
            var second = MPath.CurrentDirectory;
            first.Equals(second).ShouldBeFalse(); // they point to different directories now
        }
        finally {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    // WELL-01: Home matches Environment.GetFolderPath(UserProfile)
    [Fact]
    public void Home_MatchesUserProfile()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        MPath.Home.ToString().ShouldBe(MPath.From(expected).ToString());
    }

    // WELL-01: Temp matches Path.GetTempPath (modulo trailing-slash normalization)
    [Fact]
    public void Temp_MatchesPathGetTempPath()
    {
        var tempStr = MPath.Temp.ToString();
        tempStr.Length.ShouldBeGreaterThan(0);
        // MPath normalizes trailing slashes; either matches Path.GetTempPath sans trailing, or IS the system temp root
        var systemTemp = Path.GetTempPath();
        systemTemp.Replace('\\', '/').TrimEnd('/').ShouldBe(MPath.Temp.ToUnixString());
    }
}
