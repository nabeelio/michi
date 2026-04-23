using System.Text.Json;
using Michi.Tests.Internal;
using Shouldly;
using Xunit;

namespace Michi.Tests;

public sealed class MPathSerializationTests {
    [Fact]
    public void JsonSerializer_round_trips_host_native_absolute_path()
    {
        var original = MPath.From(
            PlatformTestHelpers.IsWindows
                    ? @"C:\work\logs\today.log"
                    : "/work/logs/today.log"
        );

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<MPath>(json);

        roundTripped.ShouldNotBeNull();
        roundTripped.ShouldBe(original);
    }

    [Fact]
    public void JsonSerializer_writes_forward_slash_payload()
    {
        var original = MPath.From(
            PlatformTestHelpers.IsWindows
                    ? @"C:\work\logs\today.log"
                    : "/work/logs/today.log"
        );

        JsonSerializer.Serialize(original)
               .ShouldBe(
                    PlatformTestHelpers.IsWindows
                            ? "\"C:/work/logs/today.log\""
                            : "\"/work/logs/today.log\""
                );
    }

    [Fact]
    public void JsonSerializer_deserializes_windows_root_payload_on_windows()
    {
        PlatformTestHelpers.SkipUnlessWindows();

        var result = JsonSerializer.Deserialize<MPath>("\"C:/work/logs/today.log\"");

        result.ShouldNotBeNull();
        result.ShouldBe(MPath.From(@"C:\work\logs\today.log"));
        result.ToString().ShouldBe(@"C:\work\logs\today.log");
    }
}
