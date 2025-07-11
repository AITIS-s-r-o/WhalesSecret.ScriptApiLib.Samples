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
/// Tests for <see cref="Program.LDcaInternal(List{Candle}, OrderRequestBuilder{MarketOrderRequest}, decimal, SymbolPair, OrderSide, decimal, TimeSpan, decimal)"/> method.
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
        ExchangeSymbolPairLimits exchangeSymbolPairLimits = new(baseVolumePrecision: 5, quoteVolumePrecision: 2, pricePrecision: 3, minBaseSize: 0.001m, minQuoteSize: 10.0m);

        Dictionary<SymbolPair, ExchangeSymbolPairLimits> symbolPairLimits = new()
        {
            { symbolPair, exchangeSymbolPairLimits },
        };

        ExchangeInfo exchangeInfo = new(ExchangeMarket.BinanceSpot, symbolPairLimits, timeShift: TimeSpan.Zero);
        this.orderRequestBuilder = new OrderRequestBuilder<MarketOrderRequest>(exchangeInfo);
    }

    /// <summary>
    /// Tests the calculation of orders without leverage (i.e. leverage is set to <c>1.0</c>).
    /// </summary>
    /// <param name="orderSide">Side of the orders.</param>
    /// <remarks>In this test we buy/sell 100 USDT worth of BTC every 5 minutes with 0.1% trade fee.</remarks>
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void NoLeverage(OrderSide orderSide)
    {
        decimal tradeFee = 0.001m;
        decimal quoteSize = 100.0m;
        TimeSpan period = TimeSpan.FromMinutes(5);
        DateTime startTimeUtc = new(year: 2025, month: 4, day: 1, hour: 0, minute: 0, second: 0, millisecond: 0, DateTimeKind.Utc);

        List<Candle> candles = new()
        {
            // First trade. Average price is (100.12 + 100.34) / 2 = 100.23 USDT. After applying base volume precision, we should get 0.99771 BTC for 100.0004733 USDT.
            new(startTimeUtc.AddMinutes(0), openPrice: 100.12m, highPrice: 100.85m, lowPrice: 99.45m, closePrice: 100.34m, baseVolume: 10.51m, quoteVolume: 1052.83m),
            new(startTimeUtc.AddMinutes(1), openPrice: 100.34m, highPrice: 101.10m, lowPrice: 99.80m, closePrice: 100.67m, baseVolume: 9.80m, quoteVolume: 983.81m),
            new(startTimeUtc.AddMinutes(2), openPrice: 100.67m, highPrice: 101.25m, lowPrice: 99.60m, closePrice: 99.95m, baseVolume: 10.21m, quoteVolume: 1025.25m),
            new(startTimeUtc.AddMinutes(3), openPrice: 99.95m, highPrice: 100.90m, lowPrice: 99.30m, closePrice: 100.45m, baseVolume: 11.00m, quoteVolume: 1103.65m),
            new(startTimeUtc.AddMinutes(4), openPrice: 100.45m, highPrice: 101.05m, lowPrice: 99.75m, closePrice: 100.88m, baseVolume: 9.51m, quoteVolume: 954.76m),

            // Second trade. Average price is (100.88 + 100.23) / 2 = 100.555 USDT. After applying base volume precision, we should get 0.99448 BTC for 99.9999364 USDT.
            new(startTimeUtc.AddMinutes(5), openPrice: 100.88m, highPrice: 101.40m, lowPrice: 100.10m, closePrice: 100.23m, baseVolume: 10.30m, quoteVolume: 1036.26m),
            new(startTimeUtc.AddMinutes(6), openPrice: 100.23m, highPrice: 100.95m, lowPrice: 99.50m, closePrice: 99.78m, baseVolume: 10.81m, quoteVolume: 1082.36m),
            new(startTimeUtc.AddMinutes(7), openPrice: 99.78m, highPrice: 100.70m, lowPrice: 99.20m, closePrice: 100.56m, baseVolume: 9.91m, quoteVolume: 993.80m),
            new(startTimeUtc.AddMinutes(8), openPrice: 100.56m, highPrice: 101.15m, lowPrice: 99.85m, closePrice: 100.92m, baseVolume: 10.15m, quoteVolume: 1019.85m),
            new(startTimeUtc.AddMinutes(9), openPrice: 100.92m, highPrice: 101.50m, lowPrice: 100.05m, closePrice: 100.31m, baseVolume: 10.70m, quoteVolume: 1075.09m),

            // Third trade. Average price is (100.31 + 99.89) / 2 = 100.1 USDT. After applying base volume precision, we should get 0.999 BTC for 99.9999 USDT.
            new(startTimeUtc.AddMinutes(10), openPrice: 100.31m, highPrice: 101.20m, lowPrice: 99.65m, closePrice: 99.89m, baseVolume: 9.60m, quoteVolume: 962.76m),
            new(startTimeUtc.AddMinutes(11), openPrice: 99.89m, highPrice: 100.80m, lowPrice: 99.25m, closePrice: 100.47m, baseVolume: 10.46m, quoteVolume: 1048.64m),
            new(startTimeUtc.AddMinutes(12), openPrice: 100.47m, highPrice: 101.30m, lowPrice: 99.90m, closePrice: 100.73m, baseVolume: 10.05m, quoteVolume: 1009.61m),
            new(startTimeUtc.AddMinutes(13), openPrice: 100.73m, highPrice: 101.45m, lowPrice: 99.95m, closePrice: 100.19m, baseVolume: 10.91m, quoteVolume: 1093.77m),
            new(startTimeUtc.AddMinutes(14), openPrice: 100.19m, highPrice: 100.90m, lowPrice: 99.40m, closePrice: 99.82m, baseVolume: 9.76m, quoteVolume: 975.03m),

            // Fourth trade. Average price is (99.82 + 103.61) / 2 = 101.715 USDT. After applying base volume precision, we should get 0.98314 BTC for 100.0000851 USDT.
            new(startTimeUtc.AddMinutes(15), openPrice: 99.82m, highPrice: 100.75m, lowPrice: 99.15m, closePrice: 103.61m, baseVolume: 10.26m, quoteVolume: 1027.66m),
            new(startTimeUtc.AddMinutes(16), openPrice: 103.61m, highPrice: 101.35m, lowPrice: 99.85m, closePrice: 100.94m, baseVolume: 9.95m, quoteVolume: 1000.95m),
            new(startTimeUtc.AddMinutes(17), openPrice: 100.94m, highPrice: 101.60m, lowPrice: 100.00m, closePrice: 100.28m, baseVolume: 10.65m, quoteVolume: 1069.64m),
        };

        LdcaResult result = Program.LDcaInternal(candles, this.orderRequestBuilder, tradeFee, symbolPair, orderSide, quoteSize, period, leverage: 1.0m);

        Assert.Equal(100.28m, result.FinalPrice);

        if (orderSide == OrderSide.Buy)
        {
            decimal amountBought = 0.99771m + 0.99448m + 0.999m + 0.98314m;
            Assert.Equal(amountBought - result.FeesPaid, result.FinalBaseBalance);

            decimal totalPaid = 100.0004733m + 99.9999364m + 99.9999m + 100.0000851m;
            Assert.Equal(-totalPaid, result.FinalQuoteBalance);
            Assert.Equal(amountBought * tradeFee, result.FeesPaid);
            Assert.Equal("BTC", result.FeeSymbol);

            decimal averageOrderPrice = totalPaid / amountBought;
            Assert.Equal(averageOrderPrice, result.AverageOrderPrice);

            decimal totalValue = (result.FinalBaseBalance * result.FinalPrice) - totalPaid;
            Assert.Equal(totalValue, result.TotalValue);

            Assert.Equal(totalPaid, result.TotalInvestedAmount);

            decimal profitPercent = totalValue / totalPaid * 100m;
            decimal epsilon = 0.0000001m;
            Assert.InRange(actual: result.ProfitPercent, low: profitPercent - epsilon, high: profitPercent + epsilon);
        }
        else
        {
            decimal amountSold = 0.99771m + 0.99448m + 0.999m + 0.98314m;
            Assert.Equal(-amountSold, result.FinalBaseBalance);

            decimal totalEarned = 100.0004733m + 99.9999364m + 99.9999m + 100.0000851m;
            Assert.Equal(totalEarned - result.FeesPaid, result.FinalQuoteBalance);
            Assert.Equal(totalEarned * tradeFee, result.FeesPaid);
            Assert.Equal("USDT", result.FeeSymbol);

            decimal averageOrderPrice = totalEarned / amountSold;
            Assert.Equal(averageOrderPrice, result.AverageOrderPrice);

            decimal totalValue = result.FinalBaseBalance + ((totalEarned - result.FeesPaid) / result.FinalPrice);
            Assert.Equal(totalValue, result.TotalValue);

            Assert.Equal(-result.FinalBaseBalance, result.TotalInvestedAmount);

            decimal profitPercent = totalValue / amountSold * 100m;
            decimal epsilon = 0.0000001m;
            Assert.InRange(actual: result.ProfitPercent, low: profitPercent - epsilon, high: profitPercent + epsilon);
        }
    }
}