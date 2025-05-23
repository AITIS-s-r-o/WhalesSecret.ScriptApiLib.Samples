using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Entities.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;

/// <summary>
/// Custom JSON converter of <see cref="OrderSide"/>.
/// </summary>
public class OrderSideConverter : JsonConverter<OrderSide>
{
    /// <inheritdoc/>
    public override OrderSide Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string at position {reader.TokenStartIndex} representing an order side.");

        string? value = reader.GetString();
        if (value is null)
            throw new JsonException("Order side cannot be null.");

        if (!Enum.TryParse(value, out OrderSide orderSide))
            throw new JsonException("Invalid order side format.");

        if (!Enum.IsDefined(orderSide))
            throw new JsonException($"'{orderSide}' does not represent a supported order side.");

        return orderSide;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, OrderSide value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}