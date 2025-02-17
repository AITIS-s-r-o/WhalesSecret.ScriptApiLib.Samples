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
/// Advanced sample that demonstrates how multiple order book subscriptions can be created and consumed at the same time using <see cref="IOrderBookSubscriptionSet"/> and its batch
/// monitoring methods. It also demonstrates a possibility to remove a subset of subscriptions from a subscription set.
/// </summary>
public class OrderBookSet : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        _ = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with a public connection.").ConfigureAwait(false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Public connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        SymbolPair[] symbolPairs = new SymbolPair[]
        {
            SymbolPair.BTC_USDT,
            SymbolPair.LTC_BTC,
            SymbolPair.LTC_USDT,
        };

        await Console.Out.WriteLineAsync($"Create order book subscriptions for {symbolPairs.Length} symbol pairs on {exchangeMarket}.").ConfigureAwait(false);
        await using IOrderBookSubscriptionSet subscriptionSet = await tradeClient.CreateOrderBookSubscriptionsAsync(symbolPairs).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"{symbolPairs.Length} order book subscriptions on {exchangeMarket} has been created successfully.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Start batch monitoring.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(timeoutCts.Token))
        {
            await Console.Out.WriteLineAsync("First we get the latest order books for all symbol pairs.").ConfigureAwait(false);
            for (int i = 0; i < symbolPairs.Length; i++)
            {
                // When we call this method for the first time, we have not consumed any order books on the given subscriptions yet, so we get the same results as we would get if
                // we called TryGetLatestOrderBook for each symbol pair separately. However, using WhenAnyNewOrderBookAsync will cause consumption of the last state and so we will
                // be able to wait for new data below.
                OrderBook orderBook = await subscriptionSet.WhenAnyNewOrderBookAsync().ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  {DateTime.UtcNow} | Latest known '{orderBook.SymbolPair}' order book:").ConfigureAwait(false);

                await OrderBookHelper.PrintOrderBookAsync(orderBook).ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            await Console.Out.WriteLineAsync("Wait for 5 order book updates from any symbol pair.").ConfigureAwait(false);
            for (int i = 0; i < 5; i++)
            {
                // Note that here, we are not guaranteed to get any number of updates from every symbol pair.
                OrderBook orderBook = await subscriptionSet.WhenAnyNewOrderBookAsync().ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  {DateTime.UtcNow} | New order book received:").ConfigureAwait(false);

                await OrderBookHelper.PrintOrderBookAsync(orderBook).ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            await Console.Out.WriteLineAsync("Dispose batch monitoring to be able to remove subscription from the set.").ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync($"Split subscription set into 2 subsets.").ConfigureAwait(false);
        if (!subscriptionSet.TryRemoveSubscriptionSubset(new SymbolPair[] { symbolPairs[1], symbolPairs[2] }, out IOrderBookSubscriptionSet? subscriptionSet2))
            throw new SanityCheckException($"Removing '{symbolPairs[1]}' and '{symbolPairs[2]}' from the subscription set failed.");

        await using IOrderBookSubscriptionSet subscriptionSet2ToDispose = subscriptionSet2;

        await Console.Out.WriteLineAsync($"Start batch monitoring for the the second subset.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(timeoutCts.Token))
        {
            await Console.Out.WriteLineAsync("Print first 10 order books for the 2 symbol pairs of the second subset.").ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                // Note that here, we are not guaranteed to get any number of updates from every symbol pair.
                OrderBook orderBook = await subscriptionSet.WhenAnyNewOrderBookAsync().ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  {DateTime.UtcNow} | New order book received:").ConfigureAwait(false);

                await OrderBookHelper.PrintOrderBookAsync(orderBook).ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            await Console.Out.WriteLineAsync("Dispose batch monitoring of the second subset.").ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("Disposing both order book subscription subsets, trade API client, and script API.").ConfigureAwait(false);
    }
}