using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;

/// <summary>
/// Custom JSON converter of <see cref="BudgetRequest"/>.
/// </summary>
public class BudgetRequestConverter : JsonConverter<BudgetRequest>
{
    /// <inheritdoc/>
    public override BudgetRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Dictionary<string, object>? budgetData = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);

        if ((budgetData is null)
            || !budgetData.TryGetValue(nameof(BudgetRequest.StrategyName), out object? strategyNameObj)
            || !budgetData.TryGetValue(nameof(BudgetRequest.PrimaryAsset), out object? primaryAssetObj)
            || !budgetData.TryGetValue(nameof(BudgetRequest.InitialBudget), out object? initialBudgetObj))
            throw new JsonException("Invalid budget request format.");

        if ((strategyNameObj is not JsonElement strategyNameElement) || (strategyNameElement.ValueKind != JsonValueKind.String))
            throw new JsonException("Strategy name must be a string.");

        if ((primaryAssetObj is not JsonElement primaryAssetElement) || (primaryAssetElement.ValueKind != JsonValueKind.String))
            throw new JsonException("Primary asset must be a string.");

        if ((initialBudgetObj is not JsonElement initialBudgetElement) || (initialBudgetElement.ValueKind != JsonValueKind.Object))
            throw new JsonException("Initial budget must be an object.");

        string? strategyName = strategyNameElement.GetString();
        if (strategyName is null)
            throw new JsonException("Strategy name must not be null.");

        string? primaryAsset = primaryAssetElement.GetString();
        if (primaryAsset is null)
            throw new JsonException("Primary asset must not be null.");

        Dictionary<string, decimal>? initialBudget = JsonSerializer.Deserialize<Dictionary<string, decimal>>(initialBudgetElement.GetRawText(), options);

        if (initialBudget is null)
            throw new JsonException("Invalid initial budget format.");

        return new(strategyName: strategyName, primaryAsset: primaryAsset, new BudgetSnapshot(initialBudget));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, BudgetRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(BudgetRequest.StrategyName));
        writer.WriteStringValue(value.StrategyName);

        writer.WritePropertyName(nameof(BudgetRequest.PrimaryAsset));
        writer.WriteStringValue(value.PrimaryAsset);

        writer.WritePropertyName(nameof(BudgetRequest.InitialBudget));
        writer.WriteStartObject();

        foreach ((string currency, decimal amount) in value.InitialBudget)
        {
            writer.WritePropertyName(currency);
            writer.WriteNumberValue(amount);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}