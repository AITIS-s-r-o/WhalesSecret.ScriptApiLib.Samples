using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Subscriptions;

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

        Console.WriteLine($"Create order book subscriptions for {symbolPairs.Length} symbol pairs on {exchangeMarket}.");
        await using IOrderBookSubscriptionSet subscriptionSet = await tradeClient.CreateOrderBookSubscriptionsAsync(symbolPairs).ConfigureAwait(false);

        Console.WriteLine($"{symbolPairs.Length} order book subscriptions on {exchangeMarket} has been created successfully.");

        Console.WriteLine("Start batch monitoring.");
        Console.WriteLine();

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(timeoutCts.Token))
        {
            Console.WriteLine("Wait for 10 order book updates from any symbol pair.");
            for (int i = 0; i < 10; i++)
            {
                // Note that we are not guaranteed to get any number of updates from any particular symbol pair. Also note that when a subscription is created, we get an initial
                // state, which is propagated as an update, but it may be preceded with any number of updates of earlier subscribed symbol pairs of the same set.
                OrderBook orderBook = await subscriptionSet.WhenAnyNewOrderBookAsync().ConfigureAwait(false);
                Console.WriteLine($"  {DateTime.UtcNow} | New order book received:");
            
                OrderBookHelper.PrintOrderBook(orderBook);
            }
            
            Console.WriteLine();

            Console.WriteLine("Dispose batch monitoring to be able to remove subscription from the set.");
        }

        Console.WriteLine("Split subscription set into 2 subsets.");
        IOrderBookSubscriptionSet? subscriptionSet2 = await subscriptionSet.TryRemoveSubscriptionSubsetAsync(new SymbolPair[] { symbolPairs[1], symbolPairs[2] })
            .ConfigureAwait(false);

        if (subscriptionSet2 is null)
            throw new SanityCheckException($"Removing '{symbolPairs[1]}' and '{symbolPairs[2]}' from the subscription set failed.");

        await using IOrderBookSubscriptionSet subscriptionSet2ToDispose = subscriptionSet2;

        Console.WriteLine("Start batch monitoring for the the second subset.");
        Console.WriteLine();

        await using (IAsyncDisposable batchMonitoring = subscriptionSet2.StartBatchMonitoring(timeoutCts.Token))
        {
            Console.WriteLine("Print first 10 order books for the 2 symbol pairs of the second subset.");
            for (int i = 0; i < 10; i++)
            {
                // Note that here, we are not guaranteed to get any number of updates from every symbol pair.
                OrderBook orderBook = await subscriptionSet2.WhenAnyNewOrderBookAsync().ConfigureAwait(false);
                Console.WriteLine($"  {DateTime.UtcNow} | New order book received:");

                OrderBookHelper.PrintOrderBook(orderBook);
            }

            Console.WriteLine();

            Console.WriteLine("Dispose batch monitoring of the second subset.");
        }

        Console.WriteLine("Disposing both order book subscription subsets, trade API client, and script API.");
    }
}