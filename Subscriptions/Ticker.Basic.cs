using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Basic sample that demonstrates how a ticker subscription can be created and consumed.
/// </summary>
public class TickerBasic : IScriptApiSample
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
        Console.WriteLine($"Create subscription for '{symbolPair}' ticker on {exchangeMarket}.");
        await using ITickerSubscription subscription = await tradeClient.CreateTickerSubscriptionAsync(symbolPair).ConfigureAwait(false);

        Console.WriteLine($"Ticker subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.");

        Console.WriteLine($"Wait for next 2 ticker updates for {symbolPair}.");
        Ticker ticker = await subscription.GetNewerTickerAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"First ticker update '{ticker}' has been received.");

        Ticker ticker2 = await subscription.GetNewerTickerAsync(timeoutCts.Token).ConfigureAwait(false);
            
        Console.WriteLine($"Second ticker update '{ticker2}' has been received.");

        Console.WriteLine("Disposing ticker subscription, trade API client, and script API.");
    }
}