using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.DCA.Converters;

/// <summary>
/// Custom JSON converter of <see cref="SymbolPair"/>.
/// </summary>
public class SymbolPairConverter : JsonConverter<SymbolPair>
{
    /// <inheritdoc/>
    public override SymbolPair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? str = reader.GetString();
        if (str is null)
            throw new JsonException("Symbol pair cannot be null.");

        if (!SymbolPair.TryParseToString(str, out SymbolPair? symbolPair))
            throw new JsonException("Invalid symbol pair format.");

        return symbolPair.Value;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, SymbolPair value, JsonSerializerOptions options)
         => throw new NotSupportedException();
}