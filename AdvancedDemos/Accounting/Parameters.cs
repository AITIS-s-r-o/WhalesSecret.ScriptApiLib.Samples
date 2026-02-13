using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;
using WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.Accounting;

/// <summary>
/// Description of program parameters.
/// </summary>
public class Parameters
{
    /// <summary>Configuration unrelated to the demo.</summary>
    public SystemConfig System { get; }

    /// <summary>Exchange market to query.</summary>
    public ExchangeMarket ExchangeMarket { get; }

    /// <summary>Inclusive UTC date of the start of the period to query.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Inclusive UTC date of the end of the period to query.</summary>
    public DateOnly EndDate { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="system">Configuration unrelated to the demo.</param>
    /// <param name="exchangeMarket">Exchange market to query.</param>
    /// <param name="startDate">Inclusive UTC date of the start of the period to query.</param>
    /// <param name="endDate">Inclusive UTC date of the end of the period to query.</param>
    /// <exception cref="InvalidArgumentException">Thrown if:
    /// <list type="bullet">
    /// <item><paramref name="startDate"/> is greater than <paramref name="endDate"/>, or</item>
    /// <item><paramref name="endDate"/> is in the future.</item>
    /// </list>
    /// </exception>
    [JsonConstructor]
    public Parameters(SystemConfig system, ExchangeMarket exchangeMarket, DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
            throw new InvalidArgumentException($"'{nameof(startDate)}' must not be greater than '{nameof(endDate)}'.", parameterName: nameof(startDate));

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (endDate > today)
            throw new InvalidArgumentException($"'{nameof(endDate)}' must not be in the future.");

        this.System = system;
        this.ExchangeMarket = exchangeMarket;
        this.StartDate = startDate;
        this.EndDate = endDate;
    }

    /// <summary>
    /// Loads parameters from a JSON file and deserializes them into a <see cref="Parameters"/> instance.
    /// </summary>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <returns>An instance of <see cref="Parameters"/> populated with data from the JSON file.</returns>
    /// <exception cref="FileAccessException">Thrown if the file cannot be read.</exception>
    /// <exception cref="JsonException">Thrown if the JSON is invalid, cannot be deserialized, or deserializes to <c>null</c>.</exception>
    public static Parameters LoadFromJson(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The specified file '{filePath}' does not exist.");
        }

        JsonSerializerOptions jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new ExchangeMarketConverter(),
            },
        };

        string jsonContent;
        try
        {
            jsonContent = File.ReadAllText(filePath);
        }
        catch (Exception e)
        {
            throw new FileAccessException($"Unable to read contents of file '{filePath}'.", e);
        }

        Parameters? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<Parameters>(jsonContent, jsonSerializerOptions);
            if (parameters is null)
                throw new JsonException($"Deserialization of contents of file '{filePath}' resulted in null.");
        }
        catch (Exception e)
        {
            if (e is JsonException)
                throw;

            throw new JsonException($"Deserialization of contents of file '{filePath}' failed.", e);
        }

        return parameters;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format
        (
            CultureInfo.InvariantCulture,
            "[{0}=`{1}`,{2}={3},{4}={5},{6}={7}]",
            nameof(this.System), this.System,
            nameof(this.ExchangeMarket), this.ExchangeMarket,
            nameof(this.StartDate), this.StartDate,
            nameof(this.EndDate), this.EndDate
        );
    }
}