using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataWedgeScanner.Web.Serialization;

/// <summary>
/// Serializes DateTimeOffset as a millisecond-precision UTC ISO-8601 string with a trailing
/// "Z" (e.g. 2026-08-30T12:00:00.000Z), instead of System.Text.Json's default round-trip
/// format (7-digit fractional seconds and a numeric +00:00 offset). Registered on the REST
/// API's JsonOptions so every timestamp the mobile client receives has one predictable shape.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString(Format));
}
