using Segments.Exceptions;
using Segments.Tests.Internal;
using Shouldly;
using Xunit;

namespace Segments.Tests;

public class SPathFormatTests {
    // Basic substitution via string.Format semantics.
    [Fact]
    public void Format_BasicSubstitution_WorksLikeStringFormat()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.Format("/home/{0}/docs", "alice");
        p.ToUnixString().ShouldBe("/home/alice/docs");
    }

    // Null template throws ArgumentNullException with a verbose actionable message.
    [Fact]
    public void Format_NullTemplate_ThrowsArgumentNullException_WithActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => SPath.Format(null!, "x"));
        ex.ParamName.ShouldBe("template");
        ex.Message.ShouldContain("string.Format-style template");
    }

    // Culture-invariant: numeric formatting doesn't use the current locale. This guards against
    // the Turkish-I trap and locale-dependent thousand separators.
    [Fact]
    public void Format_NumericArg_UsesInvariantCulture()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var p = SPath.Format("/data/{0}", 1234);
        p.ToUnixString().ShouldBe("/data/1234");
    }

    // Invalid path characters are rejected with a message naming the character and hex code.
    [Fact]
    public void Format_NullCharInSubstitution_ThrowsInvalidPathException_NamingCharAndHex()
    {
        var bad = "evil\0name";
        var ex = Should.Throw<InvalidPathException>(() => SPath.Format("/home/{0}", bad));
        ex.Message.ShouldContain("invalid path character");
        ex.Message.ShouldContain("0x0000");
        ex.AttemptedPath.ShouldContain("evil");
    }

    // Format's XML doc warns that traversal via user input IS allowed -- this test pins that
    // documented behavior. Callers handling untrusted input must validate the result.
    [Fact]
    public void Format_TraversalInUserInput_NormalizesButReachesParent()
    {
        if (PlatformTestHelpers.IsWindows)
            return;

        var userInput = "../etc";
        var p = SPath.Format("/home/{0}/docs", userInput);
        // Normalization resolves `..`, so the result escapes /home.
        p.ToUnixString().ShouldBe("/etc/docs");
    }
}
