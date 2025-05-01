using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;

/// <summary>
/// Custom JSON converter of <see cref="CandleWidth"/>.
/// </summary>
public class CandleWidthConverter : JsonConverter<CandleWidth>
{
    /// <inheritdoc/>
    public override CandleWidth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string at position {reader.TokenStartIndex} representing an order side.");

        string? value = reader.GetString();
        if (value is null)
            throw new JsonException("Order side cannot be null.");

        if (!Enum.TryParse(value, out CandleWidth candleWidth))
            throw new JsonException("Invalid candle width format.");

        if (!Enum.IsDefined(candleWidth))
            throw new JsonException($"'{candleWidth}' does not represent a supported candle width.");

        return candleWidth;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CandleWidth value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}