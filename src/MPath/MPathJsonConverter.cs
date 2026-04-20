using System.Text.Json;
using System.Text.Json.Serialization;

namespace Michi;

/// <summary>
/// Phase 1 stub. Full <see cref="MPath" /> System.Text.Json support lands in Phase 4.
/// The attribute on <see cref="MPath" /> references this type so reflection-inspecting
/// consumers see the correct metadata from v1.
/// </summary>
internal sealed class MPathJsonConverter : JsonConverter<MPath> {
    public override MPath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException(
            "MPathJsonConverter ships in Phase 4. If you see this exception, you attempted to deserialize an MPath before the serialization phase has landed."
        );
    }

    public override void Write(Utf8JsonWriter writer, MPath value, JsonSerializerOptions options)
    {
        throw new NotImplementedException(
            "MPathJsonConverter ships in Phase 4. If you see this exception, you attempted to serialize an MPath before the serialization phase has landed."
        );
    }
}
