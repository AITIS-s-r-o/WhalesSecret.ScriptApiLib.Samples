using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Subscriptions;

/// <summary>
/// Advanced sample that demonstrates how multiple ticker subscriptions can be created and consumed at the same time using <see cref="ITickerSubscriptionSet"/> and its batch
/// monitoring methods. It also demonstrates a possibility to get updates from just a single subscription of the set without altering the set.
/// </summary>
public class TickerSet : IScriptApiSample
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

        Console.WriteLine($"Create ticker subscriptions for {symbolPairs.Length} symbol pairs on {exchangeMarket}.");
        await using ITickerSubscriptionSet subscriptionSet = await tradeClient.CreateTickerSubscriptionsAsync(symbolPairs).ConfigureAwait(false);

        Console.WriteLine($"{symbolPairs.Length} ticker subscriptions on {exchangeMarket} has been created successfully.");

        Console.WriteLine("Start batch monitoring.");
        Console.WriteLine();

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(timeoutCts.Token))
        {
            Console.WriteLine("Wait for 10 ticker updates from any symbol pair.");
            for (int i = 0; i < 10; i++)
            {
                // Note that we are not guaranteed to get any number of updates for any particular symbol pair. Also note that when a subscription is created, we get an initial
                // state, which is propagated as an update, but it may be preceded with any number of updates of earlier subscribed symbol pairs of the same set.
                Ticker ticker = await subscriptionSet.WhenAnyNewTickerAsync().ConfigureAwait(false);
                Console.WriteLine($"  {DateTime.UtcNow} | New ticker update received: {ticker}");
            }

            Console.WriteLine();

            Console.WriteLine("Dispose batch monitoring to be able to call methods for individual symbol pairs.");
        }

        Console.WriteLine();

        // The methods for individual symbol pairs in the set can only be called when the batch monitoring is not active.
        Console.WriteLine($"Wait for 3 ticker updates only from symbol pair '{symbolPairs[0]}'.");

        for (int i = 0; i < 3; i++)
        {
            Ticker ticker = await subscriptionSet.GetNewerTickerAsync(symbolPairs[0]).ConfigureAwait(false);
            Console.WriteLine($"  {DateTime.UtcNow} | New ticker for symbol pair '{symbolPairs[0]}' received: {ticker}");
        }

        Console.WriteLine();

        Console.WriteLine("Disposing ticker subscription set, trade API client, and script API.");
    }
}