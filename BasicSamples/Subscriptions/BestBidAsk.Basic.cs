using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Subscriptions;

/// <summary>
/// Basic sample that demonstrates how a best bid/ask subscription can be created and consumed.
/// </summary>
public class BestBidAskBasic : IScriptApiSample
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

        SymbolPair symbolPair = SymbolPair.BTC_USDT;
        Console.WriteLine($"Create subscription for '{symbolPair}' best bid/ask updates on {exchangeMarket}.");
        await using IBestBidAskSubscription subscription = await tradeClient.CreateBestBidAskSubscriptionAsync(symbolPair).ConfigureAwait(false);

        Console.WriteLine($"Best bid/ask subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.");

        Console.WriteLine($"Wait for next 2 best bid/ask updates for {symbolPair}.");
        BestBidAsk[]? bestBidAsks = await subscription.GetNewerBestBidAsksAsync(timeoutCts.Token).ConfigureAwait(false);

        if (bestBidAsks is not null)
        {
            Console.WriteLine($"First list of best bid/ask updates with {bestBidAsks.Length} updates has been received:");
            for (int i = 0; i < bestBidAsks.Length; i++)
                Console.WriteLine($"  Best bid/ask update {i + 1}: {bestBidAsks[i]}");
        }
        else Console.WriteLine("Some best bid/ask updates are missing. Probably a connection to the exchange has been lost.");

        BestBidAsk[]? bestBidAsks2 = await subscription.GetNewerBestBidAsksAsync(timeoutCts.Token).ConfigureAwait(false);

        if (bestBidAsks2 is not null)
        {
            Console.WriteLine($"Second list of best bid/ask updates with {bestBidAsks2.Length} updates has been received:");
            for (int i = 0; i < bestBidAsks2.Length; i++)
                Console.WriteLine($"  Best bid/ask update {i + 1}: {bestBidAsks2[i]}");
        }
        else Console.WriteLine("Some best bid/ask updates are missing. Probably a connection to the exchange has been lost.");

        Console.WriteLine("Disposing best bid/ask subscription, trade API client, and script API.");
    }
}