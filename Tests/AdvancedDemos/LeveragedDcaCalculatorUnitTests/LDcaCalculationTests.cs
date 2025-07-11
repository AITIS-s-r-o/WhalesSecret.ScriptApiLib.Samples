using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.LeveragedDcaCalculator;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exchanges;
using WhalesSecret.TradeScriptLib.Utils.Orders;
using Xunit;

namespace WhalesSecret.ScriptApiLib.Samples.Tests.AdvancedDemos.LeveragedDcaCalculatorUnitTests;

/// <summary>
/// Tests for <see cref="Program.LDcaInternalAsync(List{Candle}, OrderRequestBuilder{MarketOrderRequest}, decimal, SymbolPair, OrderSide, decimal, TimeSpan, decimal)"/> method.
/// </summary>
public class LDcaCalculationTests
{
    /// <summary>Symbol pair used in the tests.</summary>
    private static readonly SymbolPair symbolPair = SymbolPair.BTC_USDT;

    /// <summary>Request builder that is used for rounding of order sizes and prices.</summary>
    private readonly OrderRequestBuilder<MarketOrderRequest> orderRequestBuilder;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    public LDcaCalculationTests()
    {
        ExchangeInfo exchangeInfo = new(ExchangeMarket.BinanceSpot, new Dictionary<SymbolPair, ExchangeSymbolPairLimits>(), timeShift: TimeSpan.Zero);
        this.orderRequestBuilder = new OrderRequestBuilder<MarketOrderRequest>(exchangeInfo);
    }

    /// <summary>
    /// Tests the calculation without leverage (i.e. leverage is set to <c>1.0</c>).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task NoLeverageAsync()
    {
        OrderSide orderSide = OrderSide.Buy;
        decimal tradeFee = 0.001m;
        decimal quoteSize = 100.0m;
        TimeSpan period = TimeSpan.FromMinutes(5);
        DateTime startTimeUtc = new(year: 2025, month: 4, day: 1, hour: 0, minute: 0, second: 0, millisecond: 0, DateTimeKind.Utc);

        List<Candle> candles = new()
        {
            new(startTimeUtc.AddMinutes(0), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(1), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(2), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(3), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(4), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(5), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(6), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(7), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(8), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(9), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(10), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(11), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(12), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(13), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(14), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(15), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(16), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
            new(startTimeUtc.AddMinutes(17), openPrice: 100m, highPrice: 105m, lowPrice: 95m, closePrice: 102m, baseVolume: 1000m, quoteVolume: 20m),
        };

        await Program.LDcaInternalAsync(candles, this.orderRequestBuilder, tradeFee, symbolPair, orderSide, quoteSize, period, leverage: 1.0m).ConfigureAwait(true);
    }
}