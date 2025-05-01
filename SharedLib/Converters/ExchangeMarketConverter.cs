using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.DCA.Converters;

/// <summary>
/// Custom JSON converter of <see cref="ExchangeMarket"/>.
/// </summary>
public class ExchangeMarketConverter : JsonConverter<ExchangeMarket>
{
    /// <inheritdoc/>
    public override ExchangeMarket Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string at position {reader.TokenStartIndex} representing an exchange market.");

        string? value = reader.GetString();
        if (value is null)
            throw new JsonException("Exchange market cannot be null.");

        if (!Enum.TryParse(value, out ExchangeMarket market))
            throw new JsonException("Invalid exchange market format.");

        if (!Enum.IsDefined(market))
            throw new JsonException($"'{market}' does not represent a supported exchange market.");

        return market;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ExchangeMarket value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}