using System;
using System.Collections.Generic;
using WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.LeveragedDcaCalculator;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exchanges;
using WhalesSecret.TradeScriptLib.Utils.Orders;
using Xunit;

namespace WhalesSecret.ScriptApiLib.Samples.Tests.AdvancedDemos.LeveragedDcaCalculatorUnitTests;

/// <summary>
/// Tests for <see cref="Program.LDcaInternal(
/// List{Candle}, OrderRequestBuilder{MarketOrderRequest}, decimal, SymbolPair, OrderSide, decimal, TimeSpan, decimal, decimal, TimeSpan)"/> method.
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

        LdcaResult result = Program.LDcaInternal(candles, this.orderRequestBuilder, tradeFee, symbolPair, orderSide, quoteSize, period, leverage: 1.0m, rolloverFee: 0m,
            rolloverPeriod: TimeSpan.Zero);

        Assert.Equal(100.28m, result.FinalPrice);

        if (orderSide == OrderSide.Buy)
        {
            decimal amountBought = 0.99771m + 0.99448m + 0.999m + 0.98314m;
            Assert.Equal(amountBought - result.TradeFeesPaid, result.FinalBaseBalance);

            decimal totalPaid = 100.0004733m + 99.9999364m + 99.9999m + 100.0000851m;
            Assert.Equal(-totalPaid, result.FinalQuoteBalance);
            Assert.Equal(amountBought * tradeFee, result.TradeFeesPaid);
            Assert.Equal("BTC", result.FeeSymbol);

            decimal averageOrderPrice = totalPaid / amountBought;
            Assert.Equal(averageOrderPrice, result.AverageOrderPrice);

            decimal totalValue = (result.FinalBaseBalance * result.FinalPrice) - totalPaid;
            Assert.Equal(totalValue, result.TotalValue);

            Assert.Equal(totalPaid, result.TotalInvestedAmount);

            decimal profitPercent = totalValue / totalPaid * 100m;
            Assert.Equal(profitPercent, result.ProfitPercent, precision: 8);
        }
        else
        {
            decimal amountSold = 0.99771m + 0.99448m + 0.999m + 0.98314m;
            Assert.Equal(-amountSold, result.FinalBaseBalance);

            decimal totalEarned = 100.0004733m + 99.9999364m + 99.9999m + 100.0000851m;
            Assert.Equal(totalEarned - result.TradeFeesPaid, result.FinalQuoteBalance);
            Assert.Equal(totalEarned * tradeFee, result.TradeFeesPaid);
            Assert.Equal("USDT", result.FeeSymbol);

            decimal averageOrderPrice = totalEarned / amountSold;
            Assert.Equal(averageOrderPrice, result.AverageOrderPrice);

            decimal totalValue = result.FinalBaseBalance + ((totalEarned - result.TradeFeesPaid) / result.FinalPrice);
            Assert.Equal(totalValue, result.TotalValue);

            Assert.Equal(-result.FinalBaseBalance, result.TotalInvestedAmount);

            decimal profitPercent = totalValue / amountSold * 100m;
            Assert.Equal(profitPercent, result.ProfitPercent, precision: 8);
        }
    }

    /// <summary>
    /// Tests the calculation of orders with leverage (i.e. leverage is greater than <c>1.0</c>).
    /// </summary>
    /// <param name="orderSide">Side of the orders.</param>
    /// <remarks>
    /// In this test we buy/sell <c>100</c> USDT worth of BTC every <c>5</c> minutes with <c>0.1</c>% trade fee. We also have a rollover fee of <c>0.2</c>% charged every <c>6</c>
    /// minutes.
    /// </remarks>
    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void Leverage(OrderSide orderSide)
    {
        decimal leverage = 10.0m;
        decimal tradeFee = 0.001m;
        decimal quoteSize = 100.0m;

        TimeSpan period = TimeSpan.FromMinutes(5);
        DateTime startTimeUtc = new(year: 2025, month: 4, day: 1, hour: 0, minute: 0, second: 0, millisecond: 0, DateTimeKind.Utc);

        decimal rolloverFee = 0.002m;
        TimeSpan rolloverPeriod = TimeSpan.FromMinutes(6);

        List<Candle> candles = new()
        {
            // First trade. Average price is (100.12 + 100.34) / 2 = 100.23 USDT. After applying base volume precision, we should get 9.97705 BTC for 999.9997215 USDT.
            // Buy order liquidation price is 100.23 * (1 - 1 / 10) = 90.207 USDT. Sell order liquidation price is 100.23 * (1 + 1 / 10) = 110.253 USDT.
            new(startTimeUtc.AddMinutes(0), openPrice: 100.12m, highPrice: 100.85m, lowPrice: 99.45m, closePrice: 100.34m, baseVolume: 10.51m, quoteVolume: 1052.83m),
            new(startTimeUtc.AddMinutes(1), openPrice: 100.34m, highPrice: 101.10m, lowPrice: 99.80m, closePrice: 100.67m, baseVolume: 9.80m, quoteVolume: 983.81m),
            new(startTimeUtc.AddMinutes(2), openPrice: 100.67m, highPrice: 101.25m, lowPrice: 99.60m, closePrice: 99.95m, baseVolume: 10.21m, quoteVolume: 1025.25m),
            new(startTimeUtc.AddMinutes(3), openPrice: 99.95m, highPrice: 100.90m, lowPrice: 99.30m, closePrice: 100.45m, baseVolume: 11.00m, quoteVolume: 1103.65m),
            new(startTimeUtc.AddMinutes(4), openPrice: 100.45m, highPrice: 101.05m, lowPrice: 99.75m, closePrice: 100.88m, baseVolume: 9.51m, quoteVolume: 954.76m),

            // Second trade. Average price is (100.88 + 100.23) / 2 = 100.555 USDT. After applying base volume precision, we should get 9.94481 BTC for 1000.00036955 USDT.
            // Buy order liquidation price is 100.555 * (1 - 1 / 10) = 90.495 USDT. Sell order liquidation price is 100.555 * (1 + 1 / 10) = 110.6105 USDT.
            new(startTimeUtc.AddMinutes(5), openPrice: 100.88m, highPrice: 101.40m, lowPrice: 100.10m, closePrice: 100.23m, baseVolume: 10.30m, quoteVolume: 1036.26m),
            new(startTimeUtc.AddMinutes(6), openPrice: 100.23m, highPrice: 100.95m, lowPrice: 99.50m, closePrice: 99.78m, baseVolume: 10.81m, quoteVolume: 1082.36m),
            new(startTimeUtc.AddMinutes(7), openPrice: 99.78m, highPrice: 100.70m, lowPrice: 99.20m, closePrice: 100.56m, baseVolume: 9.91m, quoteVolume: 993.80m),
            new(startTimeUtc.AddMinutes(8), openPrice: 100.56m, highPrice: 101.15m, lowPrice: 99.85m, closePrice: 100.92m, baseVolume: 10.15m, quoteVolume: 1019.85m),
            new(startTimeUtc.AddMinutes(9), openPrice: 100.92m, highPrice: 101.50m, lowPrice: 100.05m, closePrice: 100.31m, baseVolume: 10.70m, quoteVolume: 1075.09m),

            // Third trade. Average price is (100.31 + 99.89) / 2 = 100.1 USDT. After applying base volume precision, we should get 9.99001 BTC for 1000.000001 USDT.
            // Buy order liquidation price is 100.1 * (1 - 1 / 10) = 90.09 USDT. Sell order liquidation price is 100.1 * (1 + 1 / 10) = 110.11 USDT.
            new(startTimeUtc.AddMinutes(10), openPrice: 100.31m, highPrice: 101.20m, lowPrice: 99.65m, closePrice: 99.89m, baseVolume: 9.60m, quoteVolume: 962.76m),
            new(startTimeUtc.AddMinutes(11), openPrice: 99.89m, highPrice: 100.80m, lowPrice: 99.25m, closePrice: 100.47m, baseVolume: 10.46m, quoteVolume: 1048.64m),
            new(startTimeUtc.AddMinutes(12), openPrice: 100.47m, highPrice: 101.30m, lowPrice: 99.90m, closePrice: 100.73m, baseVolume: 10.05m, quoteVolume: 1009.61m),

            // This candle liquidates the first and the second buy orders.
            new(startTimeUtc.AddMinutes(13), openPrice: 100.73m, highPrice: 101.45m, lowPrice: 90.2m, closePrice: 100.19m, baseVolume: 10.91m, quoteVolume: 1093.77m),
            new(startTimeUtc.AddMinutes(14), openPrice: 100.19m, highPrice: 100.90m, lowPrice: 99.40m, closePrice: 99.82m, baseVolume: 9.76m, quoteVolume: 975.03m),

            // Fourth trade. Average price is (99.82 + 103.61) / 2 = 101.715 USDT. After applying base volume precision, we should get 9.83139 BTC for 999.99983385 USDT.
            // Buy order liquidation price is 101.715 * (1 - 1 / 10) = 91.5435 USDT. Sell order liquidation price is 101.715 * (1 + 1 / 10) = 111.8865 USDT.
            new(startTimeUtc.AddMinutes(15), openPrice: 99.82m, highPrice: 100.75m, lowPrice: 99.15m, closePrice: 103.61m, baseVolume: 10.26m, quoteVolume: 1027.66m),

            // This candle liquidates the first and the third sell orders.
            new(startTimeUtc.AddMinutes(16), openPrice: 103.61m, highPrice: 110.35m, lowPrice: 99.85m, closePrice: 100.94m, baseVolume: 9.95m, quoteVolume: 1000.95m),
            new(startTimeUtc.AddMinutes(17), openPrice: 100.94m, highPrice: 101.60m, lowPrice: 100.00m, closePrice: 100.28m, baseVolume: 10.65m, quoteVolume: 1069.64m),
        };

        LdcaResult result = Program.LDcaInternal(candles, this.orderRequestBuilder, tradeFee, symbolPair, orderSide, quoteSize, period, leverage: leverage,
            rolloverFee: rolloverFee, rolloverPeriod: rolloverPeriod);

        Assert.Equal(100.28m, result.FinalPrice);

        decimal position2Price = 100.555m;
        decimal position3Price = 100.1m;
        decimal position4Price = 101.715m;

        decimal position1BaseAmount = 9.97705m;
        decimal position2BaseAmount = 9.94481m;
        decimal position3BaseAmount = 9.99001m;
        decimal position4BaseAmount = 9.83139m;

        decimal position1QuoteAmount = 999.9997215m;
        decimal position2QuoteAmount = 1000.00036955m;
        decimal position3QuoteAmount = 1000.000001m;
        decimal position4QuoteAmount = 999.99983385m;

        if (orderSide == OrderSide.Buy)
        {
            Assert.Equal(0, result.FinalBaseBalance);

            // The first and the second buy orders were liquidated, so we only keep the third and the fourth buy orders.
            decimal amountKept = position3BaseAmount + position4BaseAmount;

            decimal totalValue = amountKept * result.FinalPrice;
            Assert.Equal(totalValue, result.TotalValue);

            decimal totalQuoteAmount = position1QuoteAmount + position2QuoteAmount + position3QuoteAmount + position4QuoteAmount;
            decimal tradeFeesPaid = totalQuoteAmount * tradeFee;

            // Position 1 was liquidated, we lost the full amount we paid.
            decimal position1Profit = -position1QuoteAmount / leverage;

            // Position 2 was liquidated, we lost the full amount we paid.
            decimal position2Profit = -position2QuoteAmount / leverage;

            // Position 3 was NOT liquidated, calculate profit/loss. This should be equal to difference between the final price and the buy price multiplied by the base amount,
            // i.e. this should be equal to (100.28 - 100.1) * 9.99001 = 1.7982018.
            decimal position3Profit = (result.FinalPrice - position3Price) * position3BaseAmount;
            Assert.Equal(1.7982018m, position3Profit);

            // Position 4 was NOT liquidated, calculate profit/loss. This should be equal to difference between the final price and the buy price multiplied by the base amount,
            // i.e. this should be equal to (100.28 - 101.715) * 9.83139 = -14.10804465.
            decimal position4Profit = (result.FinalPrice - position4Price) * position4BaseAmount;
            Assert.Equal(-14.10804465m, position4Profit);

            // Rollover fees are charged every 6 minutes. We have calculate the fee for every order, including the orders that have been liquidated. The first order is charged at
            // minute 6 and 12. The second order is charged at minute 11. The third order is charged at minute 16.
            decimal rolloverFeesPaid = rolloverFee * ((position1QuoteAmount * 2) + position2QuoteAmount + position3QuoteAmount);
            Assert.Equal(rolloverFeesPaid, result.RolloverFeesPaid);

            decimal finalQuoteBalance = position1Profit + position2Profit + position3Profit + position4Profit - tradeFeesPaid - rolloverFeesPaid;

            Assert.Equal(finalQuoteBalance, result.FinalQuoteBalance);
            Assert.Equal(tradeFeesPaid, result.TradeFeesPaid);
            Assert.Equal("USDT", result.FeeSymbol);

            decimal totalBaseAmount = position1BaseAmount + position2BaseAmount + position3BaseAmount + position4BaseAmount;
            decimal averageOrderPrice = totalQuoteAmount / totalBaseAmount;
            Assert.Equal(averageOrderPrice, result.AverageOrderPrice);

            decimal totalPaid = totalQuoteAmount / leverage;
            Assert.Equal(totalPaid, result.TotalInvestedAmount);

            decimal profitPercent = (finalQuoteBalance - result.TotalInvestedAmount) / result.TotalInvestedAmount * 100m;
            Assert.Equal(profitPercent, result.ProfitPercent, precision: 8);
        }
        else
        {
            Assert.Equal(0, result.FinalBaseBalance);

            // The first and the third sell orders were liquidated, so we only keep the second and the fourth sell orders.
            decimal amountKept = position2BaseAmount + position4BaseAmount;

            decimal totalValue = amountKept * result.FinalPrice;
            Assert.Equal(totalValue, result.TotalValue);

            decimal totalQuoteAmount = position1QuoteAmount + position2QuoteAmount + position3QuoteAmount + position4QuoteAmount;
            decimal tradeFeesPaid = totalQuoteAmount * tradeFee;

            // Position 1 was liquidated, we lost the full amount we paid.
            decimal position1Profit = -position1QuoteAmount / leverage;

            // Position 2 was NOT liquidated, calculate profit/loss. This should be equal to difference between the sell price and the final price multiplied by the base amount,
            // i.e. this should be equal to (100.555 - 100.28) * 9.94481 = 2.73482275.
            decimal position2Profit = (position2Price - result.FinalPrice) * position2BaseAmount;
            Assert.Equal(2.73482275m, position2Profit);

            // Position 3 was liquidated, we lost the full amount we paid.
            decimal position3Profit = -position3QuoteAmount / leverage;

            // Position 4 was NOT liquidated, calculate profit/loss.  This should be equal to difference between the sell price and the final price multiplied by the base amount,
            // i.e. this should be equal to (101.715 - 100.28) * 9.83139 = 14.10804465.
            decimal position4Profit = (position4Price - result.FinalPrice) * position4BaseAmount;
            Assert.Equal(14.10804465m, position4Profit);

            // Rollover fees are charged every 6 minutes. We have calculate the fee for every order, including the orders that have been liquidated. The first order is charged at
            // minute 6 and 12. The second order is charged at minute 11 and 17. The third order is charged at minute 16.
            decimal rolloverFeesPaid = rolloverFee * ((position1QuoteAmount * 2) + (position2QuoteAmount * 2) + position3QuoteAmount);
            Assert.Equal(rolloverFeesPaid, result.RolloverFeesPaid);

            decimal finalQuoteBalance = position1Profit + position2Profit + position3Profit + position4Profit - tradeFeesPaid - rolloverFeesPaid;

            Assert.Equal(finalQuoteBalance, result.FinalQuoteBalance);
            Assert.Equal(tradeFeesPaid, result.TradeFeesPaid);
            Assert.Equal("USDT", result.FeeSymbol);

            decimal totalBaseAmount = position1BaseAmount + position2BaseAmount + position3BaseAmount + position4BaseAmount;
            decimal averageOrderPrice = totalQuoteAmount / totalBaseAmount;
            Assert.Equal(averageOrderPrice, result.AverageOrderPrice);

            decimal totalPaid = totalQuoteAmount / leverage;
            Assert.Equal(totalPaid, result.TotalInvestedAmount);

            decimal profitPercent = (finalQuoteBalance - result.TotalInvestedAmount) / result.TotalInvestedAmount * 100m;
            Assert.Equal(profitPercent, result.ProfitPercent, precision: 8);
        }
    }
}