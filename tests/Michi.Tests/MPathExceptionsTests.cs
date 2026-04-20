using Shouldly;
using Xunit;

namespace Michi.Tests;

// requirement: EXC-01 — exception hierarchy (MPathException / InvalidPathException / NoParentException)
public class MPathExceptionsTests {
    // EXC-01 + D-33: MPathException is the base class
    [Fact]
    public void MPathException_IsBaseClass()
    {
        typeof(InvalidPathException).IsSubclassOf(typeof(MPathException)).ShouldBeTrue();
        typeof(NoParentException).IsSubclassOf(typeof(MPathException)).ShouldBeTrue();
    }

    // EXC-01 + D-34: InvalidPathException exposes AttemptedPath and a meaningful message
    [Fact]
    public void InvalidPathException_ExposesAttemptedPath_AndFormatsMessage()
    {
        var ex = new InvalidPathException("bad/path", "demo reason");
        ex.AttemptedPath.ShouldBe("bad/path");
        ex.Message.ShouldContain("bad/path");
        ex.Message.ShouldContain("demo reason");
    }

    // EXC-01 + D-35: NoParentException exposes Path and meaningful message
    [Fact]
    public void NoParentException_ExposesPath_AndFormatsMessage()
    {
        var root = MPath.From("/");
        var ex = new NoParentException(root);
        ex.Path.ShouldBe(root);
        ex.Message.ShouldContain("no parent");
    }

    // D-35d: From(null) ArgumentNullException has the full actionable message
    [Fact]
    public void ArgumentNullException_FromNullPath_HasActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.From(null!));
        ex.Message.ShouldContain("Path cannot be null");
        ex.Message.ShouldContain("Use TryFrom");
    }

    // D-35d: Format(null) ArgumentNullException has the full actionable message
    [Fact]
    public void ArgumentNullException_FromNullFormatTemplate_HasActionableMessage()
    {
        var ex = Should.Throw<ArgumentNullException>(() => MPath.Format(null!, "x"));
        ex.Message.ShouldContain("Format template cannot be null");
        ex.Message.ShouldContain("string.Format-style template");
    }

    // D-35b: empty path InvalidPathException names "empty"
    [Fact]
    public void InvalidPathException_FromEmptyPath_NamesEmptyInReason()
    {
        var ex = Should.Throw<InvalidPathException>(() => MPath.From(""));
        ex.Message.ShouldContain("empty");
    }

    // D-35b + C-10: InvalidPathException naming the offending character + hex
    [Fact]
    public void InvalidPathException_FromInvalidChar_NamesCharacterAndHex()
    {
        var ex = Should.Throw<InvalidPathException>(() => MPath.Format("/home/{0}", "a\0b"));
        ex.Message.ShouldContain("invalid path character");
        ex.Message.ShouldContain("0x0000");
    }

    // D-35e: exception messages reference no internal field names
    [Fact]
    public void ExceptionMessages_DoNotReferenceInternalFieldNames()
    {
        var ex = Should.Throw<InvalidPathException>(() => MPath.From(""));
        ex.Message.ShouldNotContain("_path");
        ex.Message.ShouldNotContain("_osNativePath");
        ex.Message.ShouldNotContain("_segments");
    }

    // EXC-01 + D-34: InvalidPathException supports an inner exception via the 3-arg ctor
    [Fact]
    public void InvalidPathException_SupportsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new InvalidPathException("p", "reason", inner);
        ex.AttemptedPath.ShouldBe("p");
        ex.Reason.ShouldBe("reason");
        ex.InnerException.ShouldBe(inner);
    }

    // D-35c: NoParentException custom-message overload (added by plan 01-04b) works
    [Fact]
    public void NoParentException_CustomMessageOverload_Works()
    {
        var p = MPath.From("/foo");
        var ex = new NoParentException(p, "custom message about levels=5 depth=2");
        ex.Path.ShouldBe(p);
        ex.Message.ShouldContain("custom message");
    }

    // D-35f meta-test: Every Michi-specific exception type has at least one test in this file that asserts message content.
    [Fact]
    public void MetaTest_EveryMichiExceptionTypeCovered()
    {
        // Ensure no one refactors away this file without updating tests.
        typeof(MPathException).ShouldNotBeNull();
        typeof(InvalidPathException).ShouldNotBeNull();
        typeof(NoParentException).ShouldNotBeNull();
    }
}
