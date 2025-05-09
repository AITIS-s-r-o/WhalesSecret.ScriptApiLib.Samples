using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhalesSecret.ScriptApiLib.Samples.SharedLib.Converters;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.MomentumBreakout;

/// <summary>
/// Description of program parameters.
/// </summary>
public class Parameters
{
    /// <summary>Maximum length of <see cref="OrderIdPrefix"/>.</summary>
    private const int MaxOrderIdPrefixLength = 8;

    /// <summary>Path to the application data folder.</summary>
    public string AppDataPath { get; }

    /// <summary>Exchange market to run the bot at.</summary>
    public ExchangeMarket ExchangeMarket { get; }

    /// <summary>Symbol pair to trade.</summary>
    public SymbolPair SymbolPair { get; }

    /// <summary>Number of candles for short-period EMA.</summary>
    public int ShortEmaLookback { get; }

    /// <summary>Number of candles for long-period EMA.</summary>
    public int LongEmaLookback { get; }

    /// <summary>Number of candles for RSI.</summary>
    public int RsiLookback { get; }

    /// <summary>Number of candles for ATR.</summary>
    public int AtrLookback { get; }

    /// <summary>Number of candles for breakout confirmation.</summary>
    public int BreakoutLookback { get; }

    /// <summary>Size of the breakout in multiples of ATR required for confirmation of the breakout.</summary>
    public decimal BreakoutAtrSize { get; }

    /// <summary>Number of candles for volume confirmation.</summary>
    public int VolumeLookback { get; }

    /// <summary>Size of the current volume in multiples of volume average over <see cref="VolumeLookback"/> period required for volume confirmation.</summary>
    public decimal VolumeAvgSize { get; }

    /// <summary>Number of candles for volatility confirmation.</summary>
    public int VolatilityLookback { get; }

    /// <summary>Size of the current ATR in multiples of the ATR average over <see cref="VolatilityLookback"/> period required for volatility confirmation.</summary>
    public decimal VolatilityAvgSize { get; }

    /// <summary>Maximum number of trades to execute per day.</summary>
    public int MaxTradesPerDay { get; }

    /// <summary>Candle width of the chart used for calculation.</summary>
    public CandleWidth CandleWidth { get; }

    /// <summary>Number of stop-loss orders for a single position.</summary>
    public int StopLossCount { get; }

    /// <summary>Number of take-profit orders for a single position.</summary>
    public int TakeProfitCount { get; }

    /// <summary>Multiple of ATR to define distance of the first stop-loss from the entry price.</summary>
    public decimal FirstStopLossAtr { get; }

    /// <summary>Multiple of ATR to define distance of the next stop-loss from the previous stop-loss.</summary>
    public decimal NextStopLossAtrIncrement { get; }

    /// <summary>Multiple of ATR to define distance of the first take-profit from the entry price.</summary>
    public decimal FirstTakeProfitAtr { get; }

    /// <summary>Multiple of ATR to define distance of the next take-profit from the previous take-profit.</summary>
    public decimal NextTakeProfitAtrIncrement { get; }

    /// <summary>Size of each trade as a multiple of the initial budget balance.</summary>
    /// <remarks>
    /// The order size for buy orders is calculated from the initial budget's allocation of the quote symbol of the <see cref="SymbolPair"/> and the order size for the sell orders
    /// is calculated from the initial budget's allocation of the base symbol. For example, if we bot is going to make an order to buy <see cref="SymbolPair.BTC_EUR"/> and the
    /// initial budget for <c>EUR</c> is <c>1,000</c> and the position size is <c>5%</c> (i.e. <c>0.05</c>), then the order size will be <c>50 EUR</c> worth of <c>BTC</c>. If the
    /// bot is going to make an order to sell <see cref="SymbolPair.BTC_EUR"/> and the initial budget for <c>BTC</c> is <c>0.01</c> and the position size is <c>5%</c>, then the
    /// order size will be <c>0.0005 BTC</c>.
    /// </remarks>
    public decimal PositionSize { get; }

    /// <summary>Number of candles to wait before allowing a new trade to be entered.</summary>
    public int TradeCooldownPeriod { get; }

    /// <summary>Prefix of client order IDs of the trading orders.</summary>
    public string OrderIdPrefix { get; }

    /// <summary>Description of budget parameters for the trading strategy.</summary>
    public BudgetRequest BudgetRequest { get; }

    /// <summary>Time period to generate the first report and between generating reports.</summary>
    public TimeSpan ReportPeriod { get; }

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <param name="appDataPath">Path to the application data folder.</param>
    /// <param name="exchangeMarket">Exchange market to DCA at.</param>
    /// <param name="symbolPair">Symbol pair to DCA.</param>
    /// <param name="shortEmaLookback">Number of candles for short-period EMA.</param>
    /// <param name="longEmaLookback">Number of candles for long-period EMA.</param>
    /// <param name="rsiLookback">Number of candles for RSI.</param>
    /// <param name="atrLookback">Number of candles for ATR.</param>
    /// <param name="breakoutLookback">Number of candles for breakout confirmation.</param>
    /// <param name="breakoutAtrSize">Size of the breakout in multiples of ATR required for confirmation of the breakout.</param>
    /// <param name="volumeLookback">Number of candles for volume confirmation.</param>
    /// <param name="volumeAvgSize">Size of the current volume in multiples of volume average over <paramref name="volumeLookback"/> period required for volume confirmation.
    /// </param>
    /// <param name="volatilityLookback">Number of candles for volatility confirmation.</param>
    /// <param name="volatilityAvgSize">Size of the current ATR in multiples of ATR average over <paramref name="volatilityLookback"/> period required for volatility confirmation.
    /// </param>
    /// <param name="maxTradesPerDay">Maximum number of trades to execute per day.</param>
    /// <param name="candleWidth">Candle width of the chart used for calculation.</param>
    /// <param name="stopLossCount">Number of stop-loss orders for a single position.</param>
    /// <param name="takeProfitCount">Number of take-profit orders for a single position.</param>
    /// <param name="firstStopLossAtr">Multiple of ATR to define distance of the first stop-loss from the entry price.</param>
    /// <param name="nextStopLossAtrIncrement">Multiple of ATR to define distance of the next stop-loss from the previous stop-loss.</param>
    /// <param name="firstTakeProfitAtr">Multiple of ATR to define distance of the first take-profit from the entry price.</param>
    /// <param name="nextTakeProfitAtrIncrement">Multiple of ATR to define distance of the next take-profit from the previous take-profit.</param>
    /// <param name="positionSize">Size of each trade as a multiple of the initial budget balance.</param>
    /// <param name="tradeCooldownPeriod">Number of candles to wait before allowing a new trade to be entered.</param>
    /// <param name="orderIdPrefix">Prefix of client order IDs of the trading orders.</param>
    /// <param name="budgetRequest">Description of budget parameters for the trading strategy.</param>
    /// <param name="reportPeriod">Time period to generate the first report and between generating reports.</param>
    /// <exception cref="InvalidArgumentException">Thrown if:
    /// <list type="bullet">
    /// <item><paramref name="appDataPath"/> is <c>null</c>, or</item>
    /// <item><paramref name="shortEmaLookback"/> is not a positive number, or</item>
    /// <item><paramref name="shortEmaLookback"/> is greater than or equal to <paramref name="longEmaLookback"/>, or</item>
    /// <item><paramref name="rsiLookback"/> is not a positive number, or</item>
    /// <item><paramref name="atrLookback"/> is not a positive number, or</item>
    /// <item><paramref name="breakoutLookback"/> is not a positive number, or</item>
    /// <item><paramref name="breakoutAtrSize"/> is not a positive number, or</item>
    /// <item><paramref name="volumeLookback"/> is not a positive number, or</item>
    /// <item><paramref name="volumeAvgSize"/> is not a positive number, or</item>
    /// <item><paramref name="volatilityLookback"/> is not a positive number, or</item>
    /// <item><paramref name="volatilityAvgSize"/> is not a positive number, or</item>
    /// <item><paramref name="maxTradesPerDay"/> is not a positive number, or</item>
    /// <item><paramref name="stopLossCount"/> is not a positive number, or</item>
    /// <item><paramref name="takeProfitCount"/> is not a positive number, or</item>
    /// <item><paramref name="stopLossCount"/> plus <paramref name="takeProfitCount"/> is greater than <see cref="IBracketedOrdersFactory.MaxBracketOrders"/>, or</item>
    /// <item><paramref name="firstStopLossAtr"/> is not a positive number, or</item>
    /// <item><paramref name="nextStopLossAtrIncrement"/> is not a positive number, or</item>
    /// <item><paramref name="firstTakeProfitAtr"/> is not a positive number, or</item>
    /// <item><paramref name="nextTakeProfitAtrIncrement"/> is not a positive number, or</item>
    /// <item><paramref name="positionSize"/> is not a positive number, or</item>
    /// <item><paramref name="tradeCooldownPeriod"/> is not a positive number, or</item>
    /// <item><paramref name="orderIdPrefix"/> is <c>null</c>, empty, or longer than <see cref="MaxOrderIdPrefixLength"/>, or</item>
    /// <item><paramref name="budgetRequest"/> has zero initial budget for either the base or the quote symbol of <paramref name="symbolPair"/>, or</item>
    /// <item><paramref name="reportPeriod"/> is not greater than <see cref="TimeSpan.Zero"/>.</item>
    /// </list>
    /// </exception>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity",
        Justification = "The cyclomatic complexity in this method is only caused by a large number of validation checks, which is fine.")]
    [JsonConstructor]
    public Parameters(string appDataPath, ExchangeMarket exchangeMarket, SymbolPair symbolPair, int shortEmaLookback, int longEmaLookback, int rsiLookback, int atrLookback,
        int breakoutLookback, decimal breakoutAtrSize, int volumeLookback, decimal volumeAvgSize, int volatilityLookback, decimal volatilityAvgSize, int maxTradesPerDay,
        CandleWidth candleWidth, int stopLossCount, int takeProfitCount, decimal firstStopLossAtr, decimal nextStopLossAtrIncrement, decimal firstTakeProfitAtr,
        decimal nextTakeProfitAtrIncrement, decimal positionSize, int tradeCooldownPeriod, string orderIdPrefix, BudgetRequest budgetRequest, TimeSpan reportPeriod)
    {
        if (appDataPath is null)
            throw new InvalidArgumentException($"'{nameof(appDataPath)}' must not be null.", parameterName: nameof(appDataPath));

        if (shortEmaLookback <= 0)
            throw new InvalidArgumentException($"'{nameof(shortEmaLookback)}' must be a positive number.", parameterName: nameof(shortEmaLookback));

        if (longEmaLookback <= shortEmaLookback)
            throw new InvalidArgumentException($"'{nameof(longEmaLookback)}' must be greater than '{nameof(shortEmaLookback)}'.", parameterName: nameof(shortEmaLookback));

        if (rsiLookback <= 0)
            throw new InvalidArgumentException($"'{nameof(rsiLookback)}' must be a positive number.", parameterName: nameof(rsiLookback));

        if (atrLookback <= 0)
            throw new InvalidArgumentException($"'{nameof(atrLookback)}' must be a positive number.", parameterName: nameof(atrLookback));

        if (breakoutLookback <= 0)
            throw new InvalidArgumentException($"'{nameof(breakoutLookback)}' must be a positive number.", parameterName: nameof(breakoutLookback));

        if (breakoutAtrSize <= 0)
            throw new InvalidArgumentException($"'{nameof(breakoutAtrSize)}' must be a positive number.", parameterName: nameof(breakoutAtrSize));

        if (volumeLookback <= 0)
            throw new InvalidArgumentException($"'{nameof(volumeLookback)}' must be a positive number.", parameterName: nameof(volumeLookback));

        if (volumeAvgSize <= 0)
            throw new InvalidArgumentException($"'{nameof(volumeAvgSize)}' must be a positive number.", parameterName: nameof(volumeAvgSize));

        if (volatilityLookback <= 0)
            throw new InvalidArgumentException($"'{nameof(volatilityLookback)}' must be a positive number.", parameterName: nameof(volatilityLookback));

        if (volatilityAvgSize <= 0)
            throw new InvalidArgumentException($"'{nameof(volatilityAvgSize)}' must be a positive number.", parameterName: nameof(volatilityAvgSize));

        if (maxTradesPerDay <= 0)
            throw new InvalidArgumentException($"'{nameof(maxTradesPerDay)}' must be a positive number.", parameterName: nameof(maxTradesPerDay));

        if (stopLossCount <= 0)
            throw new InvalidArgumentException($"'{nameof(stopLossCount)}' must be a positive number.", parameterName: nameof(stopLossCount));

        if (takeProfitCount <= 0)
            throw new InvalidArgumentException($"'{nameof(takeProfitCount)}' must be a positive number.", parameterName: nameof(takeProfitCount));

        if (stopLossCount + takeProfitCount > IBracketedOrdersFactory.MaxBracketOrders)
        {
            throw new InvalidArgumentException($"'{nameof(stopLossCount)}' + '{nameof(takeProfitCount)}' must not be greater than {IBracketedOrdersFactory.MaxBracketOrders}.",
                parameterName: nameof(stopLossCount));
        }

        if (firstStopLossAtr <= 0)
            throw new InvalidArgumentException($"'{nameof(firstStopLossAtr)}' must be a positive number.", parameterName: nameof(firstStopLossAtr));

        if (nextStopLossAtrIncrement <= 0)
            throw new InvalidArgumentException($"'{nameof(nextStopLossAtrIncrement)}' must be a positive number.", parameterName: nameof(nextStopLossAtrIncrement));

        if (firstTakeProfitAtr <= 0)
            throw new InvalidArgumentException($"'{nameof(firstTakeProfitAtr)}' must be a positive number.", parameterName: nameof(firstTakeProfitAtr));

        if (nextTakeProfitAtrIncrement <= 0)
            throw new InvalidArgumentException($"'{nameof(nextTakeProfitAtrIncrement)}' must be a positive number.", parameterName: nameof(nextTakeProfitAtrIncrement));

        if (positionSize <= 0)
            throw new InvalidArgumentException($"'{nameof(positionSize)}' must be a positive number.", parameterName: nameof(positionSize));

        if (tradeCooldownPeriod <= 0)
            throw new InvalidArgumentException($"'{nameof(tradeCooldownPeriod)}' must be a positive number.", parameterName: nameof(tradeCooldownPeriod));

        if (string.IsNullOrEmpty(orderIdPrefix) || (orderIdPrefix.Length > MaxOrderIdPrefixLength))
        {
            throw new InvalidArgumentException($"'{nameof(orderIdPrefix)}' must not be null, empty, nor longer than {MaxOrderIdPrefixLength}.",
                parameterName: nameof(MaxOrderIdPrefixLength));
        }

        if (!budgetRequest.InitialBudget.TryGetValue(symbolPair.BaseSymbol, out decimal baseAllocation) || (baseAllocation <= 0))
        {
            throw new InvalidArgumentException($"Initial budget for '{symbolPair.BaseSymbol}' in '{nameof(budgetRequest)}' be a positive number.",
                parameterName: nameof(budgetRequest));
        }

        if (!budgetRequest.InitialBudget.TryGetValue(symbolPair.QuoteSymbol, out decimal quoteAllocation) || (quoteAllocation <= 0))
        {
            throw new InvalidArgumentException($"Initial budget for '{symbolPair.QuoteSymbol}' in '{nameof(budgetRequest)}' be a positive number.",
                parameterName: nameof(budgetRequest));
        }

        if (reportPeriod <= TimeSpan.Zero)
            throw new InvalidArgumentException($"'{nameof(reportPeriod)}' must be greater than {TimeSpan.Zero}.", parameterName: nameof(reportPeriod));

        this.AppDataPath = appDataPath;
        this.ExchangeMarket = exchangeMarket;
        this.SymbolPair = symbolPair;
        this.ShortEmaLookback = shortEmaLookback;
        this.LongEmaLookback = longEmaLookback;
        this.RsiLookback = rsiLookback;
        this.AtrLookback = atrLookback;
        this.BreakoutLookback = breakoutLookback;
        this.BreakoutAtrSize = breakoutAtrSize;
        this.VolumeLookback = volumeLookback;
        this.VolumeAvgSize = volumeAvgSize;
        this.VolatilityLookback = volatilityLookback;
        this.VolatilityAvgSize = volatilityAvgSize;
        this.MaxTradesPerDay = maxTradesPerDay;
        this.CandleWidth = candleWidth;
        this.StopLossCount = stopLossCount;
        this.TakeProfitCount = takeProfitCount;
        this.FirstStopLossAtr = firstStopLossAtr;
        this.NextStopLossAtrIncrement = nextStopLossAtrIncrement;
        this.FirstTakeProfitAtr = firstTakeProfitAtr;
        this.NextTakeProfitAtrIncrement = nextTakeProfitAtrIncrement;
        this.PositionSize = positionSize;
        this.TradeCooldownPeriod = tradeCooldownPeriod;
        this.OrderIdPrefix = orderIdPrefix;
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
                new CandleWidthConverter(),
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
        string format = "[{0}=`{1}`,{2}={3},{4}=`{5}`,{6}={7},{8}={9},{10}={11},{12}={13},{14}={15},{16}={17},{18}={19},{20}={21},{22}={23},{24}={25},{26}={27},{28}={29},{30}={31}"
            + ",{32}={33},{34}={35},{36}={37},{38}={39},{40}={41},{42}={43},{44}={45},{46}=`{47}`,{48}='{49}',{50}={51}]";
        return string.Format
        (
            CultureInfo.InvariantCulture,
            format,
            nameof(this.AppDataPath), this.AppDataPath,
            nameof(this.ExchangeMarket), this.ExchangeMarket,
            nameof(this.SymbolPair), this.SymbolPair,
            nameof(this.ShortEmaLookback), this.ShortEmaLookback,
            nameof(this.LongEmaLookback), this.LongEmaLookback,
            nameof(this.RsiLookback), this.RsiLookback,
            nameof(this.AtrLookback), this.AtrLookback,
            nameof(this.BreakoutLookback), this.BreakoutLookback,
            nameof(this.BreakoutAtrSize), this.BreakoutAtrSize,
            nameof(this.VolumeLookback), this.VolumeLookback,
            nameof(this.VolumeAvgSize), this.VolumeAvgSize,
            nameof(this.VolatilityLookback), this.VolatilityLookback,
            nameof(this.VolatilityAvgSize), this.VolatilityAvgSize,
            nameof(this.MaxTradesPerDay), this.MaxTradesPerDay,
            nameof(this.CandleWidth), this.CandleWidth,
            nameof(this.StopLossCount), this.StopLossCount,
            nameof(this.TakeProfitCount), this.TakeProfitCount,
            nameof(this.FirstStopLossAtr), this.FirstStopLossAtr,
            nameof(this.NextStopLossAtrIncrement), this.NextStopLossAtrIncrement,
            nameof(this.FirstTakeProfitAtr), this.FirstTakeProfitAtr,
            nameof(this.NextTakeProfitAtrIncrement), this.NextTakeProfitAtrIncrement,
            nameof(this.PositionSize), this.PositionSize,
            nameof(this.TradeCooldownPeriod), this.TradeCooldownPeriod,
            nameof(this.OrderIdPrefix), this.OrderIdPrefix,
            nameof(this.BudgetRequest), this.BudgetRequest,
            nameof(this.ReportPeriod), this.ReportPeriod
        );
    }
}