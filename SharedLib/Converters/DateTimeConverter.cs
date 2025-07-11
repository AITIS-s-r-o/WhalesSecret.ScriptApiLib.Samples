using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;

/// <summary>
/// Custom JSON converter of <see cref="DateTime"/>.
/// </summary>
public class DateTimeConverter : JsonConverter<DateTime>
{
    /// <summary>Format of the date time.</summary>
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc/>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? str = reader.GetString();
        if (str is null)
            throw new JsonException("Date time cannot be null.");

        if (!DateTime.TryParseExact(str, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime dateTime))
            throw new JsonException($"Invalid date time format, '{DateTimeFormat}' expected.");

        return dateTime;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
         => throw new NotSupportedException();
}