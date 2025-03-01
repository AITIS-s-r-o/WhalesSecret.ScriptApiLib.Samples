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

        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        _ = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with a public connection.").ConfigureAwait(false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Public connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        SymbolPair symbolPair = SymbolPair.BTC_USDT;
        await Console.Out.WriteLineAsync($"Create subscription for '{symbolPair}' candlesticks on {exchangeMarket}.").ConfigureAwait(false);
        await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(symbolPair).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Candlestick subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.")
            .ConfigureAwait(false);

        CandleWidth candleWidth = CandleWidth.Minute1;

        Candle lastClosedCandle = subscription.GetLatestClosedCandlestick(candleWidth);
        await Console.Out.WriteLineAsync($"Latest closed {candleWidth} candle: {lastClosedCandle}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Getting 24 hours of candle data").ConfigureAwait(false);
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

        await this.ReportRsiAsync(quotes).ConfigureAwait(false);

        // Loop until timeout.
        while (!timeoutCts.IsCancellationRequested)
        {
            await Console.Out.WriteLineAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync("Waiting for the next closed candle...").ConfigureAwait(false);

            try
            {
                lastClosedCandle = await subscription.WaitNextClosedCandlestickAsync(candleWidth, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await Console.Out.WriteLineAsync($"New closed candle arrived: {lastClosedCandle}").ConfigureAwait(false);

            quotes.Add(this.QuoteFromCandle(lastClosedCandle));
            await this.ReportRsiAsync(quotes).ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("Disposing candlestick subscription, trade API client, and script API.").ConfigureAwait(false);
    }

    /// <summary>
    /// Prints information about current RSI on the console output.
    /// </summary>
    /// <param name="quotes">List of quotes to calculate RSI from.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task ReportRsiAsync(IEnumerable<Quote> quotes)
    {
        IEnumerable<RsiResult> results = quotes.GetRsi();
        RsiResult lastRsi = results.Last();

        string interpretation = lastRsi.Rsi switch
        {
            < 30 => " (oversold!)",
            > 70 => " (overbought!)",
            _ => string.Empty
        };

        await Console.Out.WriteLineAsync($"Current RSI: {lastRsi.Date} -> {lastRsi.Rsi}{interpretation}").ConfigureAwait(false);
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