using System.Text.Json;
using System.Text.Json.Serialization;
using Michi.Exceptions;

namespace Michi.Converters;

/// <summary>
/// <see cref="System.Text.Json" /> converter for <see cref="MPath" />. Serializes to the
/// canonical forward-slash string form (via <see cref="MPath.ToUnixString" />) so payloads
/// are portable across Windows, macOS, and Linux; deserializes via
/// <see cref="MPath.From(string, MPathOptions?)" />.
/// </summary>
/// <remarks>
/// Forward-slash output is deliberate and independent of host OS: without it, an MPath
/// serialized on Windows would contain backslashes, which JSON consumers on Unix would
/// then treat as escape characters. Registered automatically via
/// <see cref="JsonConverterAttribute" /> on <see cref="MPath" /> -- callers do not need to
/// add this converter to <see cref="JsonSerializerOptions.Converters" />.
/// </remarks>
internal sealed class MPathJsonConverter : JsonConverter<MPath> {
    public override MPath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String) {
            throw new JsonException($"Expected a JSON string for MPath, got {reader.TokenType}.");
        }

        var raw = reader.GetString();
        if (raw is null) {
            return null;
        }

        try {
            return MPath.From(raw);
        } catch (InvalidPathException ex) {
            throw new JsonException($"Cannot deserialize '{raw}' as MPath: {ex.Message}", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, MPath value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUnixString());
}
