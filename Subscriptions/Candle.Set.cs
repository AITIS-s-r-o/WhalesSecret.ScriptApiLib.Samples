using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Advanced sample that demonstrates how multiple candlestick subscriptions can be created and consumed at the same time using <see cref="ICandlestickSubscriptionSet"/> and its
/// batch monitoring methods. It also demonstrates a possibility to remove a subscription from the set.
/// </summary>
public class CandleSet : IScriptApiSample
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

        SymbolPair[] symbolPairs = new SymbolPair[]
        {
            SymbolPair.BTC_USDT,
            SymbolPair.LTC_BTC,
            SymbolPair.LTC_USDT,
        };

        Console.WriteLine($"Create candlestick subscriptions for {symbolPairs.Length} symbol pairs on {exchangeMarket}.");
        await using ICandlestickSubscriptionSet subscriptionSet = await tradeClient.CreateCandlestickSubscriptionsAsync(symbolPairs).ConfigureAwait(false);

        Console.WriteLine($"{symbolPairs.Length} candlestick subscriptions on {exchangeMarket} has been created successfully.");

        CandleWidth candleWidth = CandleWidth.Minute1;

        Console.WriteLine($"Start batch monitoring for candle width {candleWidth}.");
        Console.WriteLine();

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(candleWidth, timeoutCts.Token))
        {
            // Candles are either open or closed. A closed candle is a candle for a time period that will not change in the future because its time interval is in the past.
            // Similarly, an open candle is a candle that represents an ongoing time period and as such it can still change.
            Console.WriteLine("Wait for 6 closed candlesticks updates.");
            for (int i = 0; i < 6; i++)
            {
                // Note that we are not guaranteed to get two closed candles for each of the three symbol pairs in the subscription. Some exchanges do not deliver closed
                // candlestick updates until there is an actual new trade. Also note that when a subscription is created, we get an initial state, which is propagated as an update,
                // but it may be preceded with any number of updates of earlier subscribed symbol pairs of the same set.
                CandleWithExchangeSymbolPair candle = await subscriptionSet.WhenAnyNewClosedCandlestickAsync(candleWidth).ConfigureAwait(false);
                Console.WriteLine($"  {DateTime.UtcNow} | New closed candle received: {candle}");
            }

            Console.WriteLine();

            Console.WriteLine("Dispose batch monitoring to be able to remove subscription from the set.");
        }

        SymbolPair removeSymbolPair = symbolPairs[1];
        Console.WriteLine($"Remove and dispose '{removeSymbolPair}' subscription from the set.");

        if (!subscriptionSet.TryRemoveSubscription(removeSymbolPair, out ICandlestickSubscription? subscription))
            throw new SanityCheckException($"Subscription for '{removeSymbolPair}' was not found in the set.");

        await subscription.DisposeAsync().ConfigureAwait(false);

        Console.WriteLine();

        Console.WriteLine($"Start batch monitoring again for candle width {candleWidth}.");
        Console.WriteLine();

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(candleWidth, timeoutCts.Token))
        {
            Console.WriteLine("Wait for 10 candlesticks updates from any symbol pair of the two remaining symbols.");
            for (int i = 0; i < 10; i++)
            {
                CandleUpdate candleUpdate = await subscriptionSet.WhenAnyNewCandlestickUpdateAsync(candleWidth).ConfigureAwait(false);
                Console.WriteLine($"  {DateTime.UtcNow} | New candle update received: {candleUpdate}");
            }

            Console.WriteLine();
            Console.WriteLine("Dispose batch monitoring to.");
        }

        Console.WriteLine("Disposing candlestick subscriptions set, trade API client, and script API.");
    }
}