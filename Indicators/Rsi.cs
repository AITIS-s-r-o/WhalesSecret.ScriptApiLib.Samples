using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;

namespace WhalesSecret.ScriptApiLib.Samples.Indicators;

/// <summary>
/// Sample that demonstrates the use of <see cref="https://dotnet.stockindicators.dev/indicators/Rsi/#content">Relative Strength Indicator</see> (RSI) from
/// <see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see>. The sample creates a candlestick subscriptions with 1-minute BTC/USDT candles and feeds
/// the indicator for about 5 minutes. It also obtains historical candle data for the last 24 hours.
/// </summary>
public class Rsi : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(5));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Connect to {exchangeMarket} exchange with a public connection.");
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        Console.WriteLine($"Public connection to {exchangeMarket} has been established successfully.");

        SymbolPair symbolPair = SymbolPair.BTC_USDT;
        Console.WriteLine($"Create subscription for '{symbolPair}' candlesticks on {exchangeMarket}.");
        await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(symbolPair).ConfigureAwait(false);

        Console.WriteLine($"Candlestick subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.");

        CandleWidth candleWidth = CandleWidth.Minute1;

        Candle lastClosedCandle = subscription.GetLatestClosedCandlestick(candleWidth);
        Console.WriteLine($"Latest closed {candleWidth} candle: {lastClosedCandle}");

        Console.WriteLine("Getting 24 hours of candle data");
        DateTime startTime = lastClosedCandle.Timestamp.AddHours(-24);

        // End time is exclusive, so to make sure the last closed candle is included, we add 1 second. 
        DateTime endTime = lastClosedCandle.Timestamp.AddSeconds(1);

        CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(symbolPair, candleWidth, startTime: startTime, endTime: endTime, timeoutCts.Token)
            .ConfigureAwait(false);

        List<Quote> quotes = new(capacity: candlestickData.Candles.Count + 100);
        foreach (Candle candle in candlestickData.Candles)
            quotes.Add(this.QuoteFromCandle(candle));

        Console.WriteLine();
        Console.WriteLine($"Last closed candle: {lastClosedCandle}");

        this.ReportRsi(quotes);

        // Loop until timeout.
        while (!timeoutCts.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine("Waiting for the next closed candle...");

            try
            {
                lastClosedCandle = await subscription.WaitNextClosedCandlestickAsync(candleWidth, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Console.WriteLine($"New closed candle arrived: {lastClosedCandle}");

            quotes.Add(this.QuoteFromCandle(lastClosedCandle));
            this.ReportRsi(quotes);
        }

        Console.WriteLine("Disposing candlestick subscription, trade API client, and script API.");
    }

    /// <summary>
    /// Prints information about current RSI on the console output.
    /// </summary>
    /// <param name="quotes">List of quotes to calculate RSI from.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private void ReportRsi(IEnumerable<Quote> quotes)
    {
        IEnumerable<RsiResult> results = quotes.GetRsi();
        RsiResult lastRsi = results.Last();

        string interpretation = lastRsi.Rsi switch
        {
            < 30 => " (oversold!)",
            > 70 => " (overbought!)",
            _ => string.Empty
        };

        Console.WriteLine($"Current RSI: {lastRsi.Date} -> {lastRsi.Rsi}{interpretation}");
    }

    /// <summary>
    /// Converts Whale's Secret candle representation to OHLCV data format for <see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see>.
    /// </summary>
    /// <param name="candle">Whale's Secret candle to convert.</param>
    /// <returns><see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see> quote representing the candle.</returns>
    private Quote QuoteFromCandle(Candle candle)
    {
        Quote quote = new()
        {
            Date = candle.Timestamp,
            Open = candle.OpenPrice,
            High = candle.HighPrice,
            Low = candle.LowPrice,
            Close = candle.ClosePrice,
            Volume = candle.BaseVolume,
        };

        return quote;
    }
}