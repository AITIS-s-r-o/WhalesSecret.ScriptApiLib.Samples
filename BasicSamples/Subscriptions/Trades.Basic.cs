using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Subscriptions;

/// <summary>
/// Basic sample that demonstrates how a trades subscription can be created and consumed.
/// </summary>
public class TradesBasic : IScriptApiSample
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
        Console.WriteLine($"Create subscription for '{symbolPair}' trade on {exchangeMarket}.");
        await using ITradeSubscription subscription = await tradeClient.CreateTradeSubscriptionAsync(symbolPair).ConfigureAwait(false);

        Console.WriteLine($"Trade subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.");

        Console.WriteLine($"Wait for next 2 trade updates for {symbolPair}.");
        Trade[]? trades = await subscription.GetNewerTradesAsync(timeoutCts.Token).ConfigureAwait(false);

        if (trades is not null)
        {
            Console.WriteLine($"First list of trade updates with {trades.Length} trades has been received:");
            for (int i = 0; i < trades.Length; i++)
                Console.WriteLine($"  Trade update {i + 1}: {trades[i]}");
        }
        else Console.WriteLine("Some trade updates are missing. Probably a connection to the exchange has been lost.");

        Trade[]? trades2 = await subscription.GetNewerTradesAsync(timeoutCts.Token).ConfigureAwait(false);

        if (trades2 is not null)
        {
            Console.WriteLine($"Second list of trade updates with {trades2.Length} trades has been received:");
            for (int i = 0; i < trades2.Length; i++)
                Console.WriteLine($"  Trade update {i + 1}: {trades2[i]}");
        }
        else Console.WriteLine("Some trade updates are missing. Probably a connection to the exchange has been lost.");

        Console.WriteLine("Disposing trade subscription, trade API client, and script API.");
    }
}