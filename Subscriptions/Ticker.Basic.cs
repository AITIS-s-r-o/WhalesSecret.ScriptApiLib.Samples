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

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with a public connection.").ConfigureAwait(false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Public connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        SymbolPair symbolPair = SymbolPair.BTC_USDT;
        await Console.Out.WriteLineAsync($"Create subscription for '{symbolPair}' ticker on {exchangeMarket}.").ConfigureAwait(false);
        await using ITickerSubscription subscription = await tradeClient.CreateTickerSubscriptionAsync(symbolPair).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Ticker subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Wait for next 2 ticker updates for {symbolPair}.").ConfigureAwait(false);
        Ticker ticker = await subscription.GetNewerTickerAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"First ticker update '{ticker}' has been received.").ConfigureAwait(false);

        Ticker ticker2 = await subscription.GetNewerTickerAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Second ticker update '{ticker2}' has been received.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing ticker subscription, trade API client, and script API.").ConfigureAwait(false);
    }
}