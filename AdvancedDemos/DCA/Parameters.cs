using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;
using WhalesSecret.ScriptApiLib.Samples.SharedLib.SystemSettings;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.DCA;

/// <summary>
/// Description of program parameters.
/// </summary>
public class Parameters
{
    /// <summary>Configuration of trading bots unrelated to the bot strategy.</summary>
    public SystemConfig System { get; }

    /// <summary>Exchange market to execute dollar-cost average (DCA) strategy at.</summary>
    public ExchangeMarket ExchangeMarket { get; }

    /// <summary>Symbol pair to DCA.</summary>
    public SymbolPair SymbolPair { get; }

    /// <summary>Time period in between the orders.</summary>
    public TimeSpan Period { get; }

    /// <summary>Order size in quote symbol.</summary>
    public decimal QuoteSize { get; }

    /// <summary>Order side.</summary>
    public OrderSide OrderSide { get; }

    /// <summary>Description of budget parameters for the trading strategy.</summary>
    public BudgetRequest BudgetRequest { get; }

    /// <summary>Time period to generate the first report and between generating reports.</summary>
    public TimeSpan ReportPeriod { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="system">Configuration of trading bots unrelated to the bot strategy.</param>
    /// <param name="exchangeMarket">Exchange market to DCA at.</param>
    /// <param name="symbolPair">Symbol pair to DCA.</param>
    /// <param name="period">Time period in between the orders.</param>
    /// <param name="quoteSize">Order size in quote symbol.</param>
    /// <param name="orderSide">Order side.</param>
    /// <param name="budgetRequest">Description of budget parameters for the trading strategy.</param>
    /// <param name="reportPeriod">Time period to generate the first report and between generating reports.</param>
    /// <exception cref="InvalidArgumentException">Thrown if:
    /// <list type="bullet">
    /// <item><paramref name="period"/> is not greater than <see cref="TimeSpan.Zero"/>, or</item>
    /// <item><paramref name="quoteSize"/> is not a positive number, or</item>
    /// <item><paramref name="budgetRequest"/> has zero initial budget for the base symbol of <paramref name="symbolPair"/> and <paramref name="orderSide"/> is
    /// <see cref="OrderSide.Sell"/>; or it has zero initial budget for the quote symbol of <paramref name="symbolPair"/> and <paramref name="orderSide"/> is
    /// <see cref="OrderSide.Buy"/>.</item>
    /// <item><paramref name="reportPeriod"/> is not greater than <see cref="TimeSpan.Zero"/>.</item>
    /// </list>
    /// </exception>
    [JsonConstructor]
    public Parameters(SystemConfig system, ExchangeMarket exchangeMarket, SymbolPair symbolPair, TimeSpan period, decimal quoteSize, OrderSide orderSide,
        BudgetRequest budgetRequest, TimeSpan reportPeriod)
    {
        if (period <= TimeSpan.Zero)
            throw new InvalidArgumentException($"'{nameof(period)}' must be greater than {TimeSpan.Zero}.", parameterName: nameof(period));

        if (quoteSize <= 0)
            throw new InvalidArgumentException($"'{nameof(quoteSize)}' must be a positive number.", parameterName: nameof(quoteSize));

        if (orderSide == OrderSide.Sell)
        {
            if (!budgetRequest.InitialBudget.TryGetValue(symbolPair.BaseSymbol, out decimal baseAllocation) || (baseAllocation <= 0))
            {
                throw new InvalidArgumentException($"Initial budget for '{symbolPair.BaseSymbol}' in '{nameof(budgetRequest)}' be a positive number.",
                    parameterName: nameof(budgetRequest));
            }
        }

        if (orderSide == OrderSide.Buy)
        {
            if (!budgetRequest.InitialBudget.TryGetValue(symbolPair.QuoteSymbol, out decimal quoteAllocation) || (quoteAllocation <= 0))
            {
                throw new InvalidArgumentException($"Initial budget for '{symbolPair.QuoteSymbol}' in '{nameof(budgetRequest)}' be a positive number.",
                    parameterName: nameof(budgetRequest));
            }
        }

        if (reportPeriod <= TimeSpan.Zero)
            throw new InvalidArgumentException($"'{nameof(reportPeriod)}' must be greater than {TimeSpan.Zero}.", parameterName: nameof(reportPeriod));

        this.System = system;
        this.ExchangeMarket = exchangeMarket;
        this.SymbolPair = symbolPair;
        this.Period = period;
        this.QuoteSize = quoteSize;
        this.OrderSide = orderSide;
        this.BudgetRequest = budgetRequest;
        this.ReportPeriod = reportPeriod;
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
                new BudgetRequestConverter(),
                new ExchangeMarketConverter(),
                new OrderSideConverter(),
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
            "[{0}=`{1}`,{2}={3},{4}=`{5}`,{6}={7},{8}={9},{10}={11},{12}=`{13}`,{14}={15}]",
            nameof(this.System), this.System,
            nameof(this.ExchangeMarket), this.ExchangeMarket,
            nameof(this.SymbolPair), this.SymbolPair,
            nameof(this.Period), this.Period,
            nameof(this.QuoteSize), this.QuoteSize,
            nameof(this.OrderSide), this.OrderSide,
            nameof(this.BudgetRequest), this.BudgetRequest,
            nameof(this.ReportPeriod), this.ReportPeriod
        );
    }
}