using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.LeveragedDcaCalculator;

/// <summary>
/// Description of program parameters.
/// </summary>
public class Parameters
{
    /// <summary>Path to the application data folder.</summary>
    public string AppDataPath { get; }

    /// <summary>Exchange market to DCA at.</summary>
    public ExchangeMarket ExchangeMarket { get; }

    /// <summary>Symbol pair to DCA.</summary>
    public SymbolPair SymbolPair { get; }

    /// <summary>Inclusive UTC time of the start of the time-frame.</summary>
    public DateTime StartTimeUtc { get; }

    /// <summary>Exclusive UTC time of the end of the time-frame.</summary>
    public DateTime EndTimeUtc { get; }

    /// <summary>Time period in between the orders.</summary>
    public TimeSpan Period { get; }

    /// <summary>Order size in quote symbol.</summary>
    public decimal QuoteSize { get; }

    /// <summary>Order side.</summary>
    public OrderSide OrderSide { get; }

    /// <summary>Trading fee in percent.</summary>
    public decimal TradeFeePercent { get; }

    /// <summary>Leverage of the trades. It must be a decimal number greater than or equal to <c>1.0</c>. Set to <c>1.0</c> to calculate normal DCA without leverage.</summary>
    public decimal Leverage { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="appDataPath">Path to the application data folder.</param>
    /// <param name="exchangeMarket">Exchange market to DCA at.</param>
    /// <param name="symbolPair">Symbol pair to DCA.</param>
    /// <param name="startTimeUtc">Inclusive UTC time of the start of the time-frame.</param>
    /// <param name="endTimeUtc">Exclusive UTC time of the end of the time-frame.</param>
    /// <param name="period">Time period in between the orders.</param>
    /// <param name="quoteSize">Order size in quote symbol.</param>
    /// <param name="orderSide">Order side.</param>
    /// <param name="tradeFeePercent">Trading fee in percent.</param>
    /// <param name="leverage">Leverage of the trades. It must be a decimal number greater than or equal to <c>1.0</c>. Set to <c>1.0</c> to calculate normal DCA without leverage.
    /// </param>
    /// <exception cref="InvalidArgumentException">Thrown if:
    /// <list type="bullet">
    /// <item><paramref name="appDataPath"/> is <c>null</c> or empty, or</item>
    /// <item><paramref name="period"/> is not greater than <see cref="TimeSpan.Zero"/>, or</item>
    /// <item><paramref name="quoteSize"/> is not a positive number, or</item>
    /// <item><paramref name="leverage"/> is smaller than <c>1.0</c>.</item>
    /// </list>
    /// </exception>
    [JsonConstructor]
    public Parameters(string appDataPath, ExchangeMarket exchangeMarket, SymbolPair symbolPair, DateTime startTimeUtc, DateTime endTimeUtc, TimeSpan period, decimal quoteSize,
        OrderSide orderSide, decimal tradeFeePercent, decimal leverage)
    {
        if (string.IsNullOrEmpty(appDataPath))
            throw new InvalidArgumentException($"'{nameof(appDataPath)}' must not be null or empty.", parameterName: nameof(appDataPath));

        if (period <= TimeSpan.Zero)
            throw new InvalidArgumentException($"'{nameof(period)}' must be greater than {TimeSpan.Zero}.", parameterName: nameof(period));

        if (quoteSize <= 0)
            throw new InvalidArgumentException($"'{nameof(quoteSize)}' must be a positive number.", parameterName: nameof(quoteSize));

        if (leverage <= 1.0m)
            throw new InvalidArgumentException($"'{nameof(leverage)}' must not be smaller than 1.0.", parameterName: nameof(leverage));

        this.AppDataPath = appDataPath;
        this.ExchangeMarket = exchangeMarket;
        this.SymbolPair = symbolPair;
        this.StartTimeUtc = startTimeUtc;
        this.EndTimeUtc = endTimeUtc;
        this.Period = period;
        this.QuoteSize = quoteSize;
        this.OrderSide = orderSide;
        this.TradeFeePercent = tradeFeePercent;
        this.Leverage = leverage;
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
                new SymbolPairConverter(),
                new ExchangeMarketConverter(),
                new OrderSideConverter(),
                new DateTimeConverter(),
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
            "[{0}=`{1}`,{2}={3},{4}=`{5}`,{6}={7},{8}={9},{10}={11},{12}={13},{14}={15},{16}={17},{18}={19}]",
            nameof(this.AppDataPath), this.AppDataPath,
            nameof(this.ExchangeMarket), this.ExchangeMarket,
            nameof(this.SymbolPair), this.SymbolPair,
            nameof(this.StartTimeUtc), this.StartTimeUtc,
            nameof(this.EndTimeUtc), this.EndTimeUtc,
            nameof(this.Period), this.Period,
            nameof(this.QuoteSize), this.QuoteSize,
            nameof(this.OrderSide), this.OrderSide,
            nameof(this.TradeFeePercent), this.TradeFeePercent,
            nameof(this.Leverage), this.Leverage
        );
    }
}