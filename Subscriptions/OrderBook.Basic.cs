using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Tests of order book subscriptions with a trade API client in the script-box-less environment.
/// </summary>
public class OrderBookBasic : IScriptApiSample
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

        SymbolPair symbolPair = SymbolPair.BTC_USDT;
        await Console.Out.WriteLineAsync($"Create subscription for '{symbolPair}' order book on {exchangeMarket}.").ConfigureAwait(false);
        await using IOrderBookSubscription subscription = await tradeClient.CreateOrderBookSubscriptionAsync(symbolPair).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Order book subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.")
            .ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Wait for next order book update for {symbolPair}.").ConfigureAwait(false);
        OrderBook orderBook = await subscription.GetOrderBookAsync(getMode: OrderBookGetMode.WaitUntilNew, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Up-to-date order book snapshot '{orderBook}' has been received.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        for (int i = 4; i >= 0; i--)
        {
            if (orderBook.Asks.Count < i)
                continue;

            await Console.Out.WriteLineAsync($"  ask #{i + 1}: {orderBook.Asks[i].Quantity} {symbolPair.BaseSymbol} @ {orderBook.Asks[i].Price} {symbolPair.QuoteSymbol}")
                .ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("  ---------------------------------------").ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
        {
            if (orderBook.Bids.Count < i)
                break;

            await Console.Out.WriteLineAsync($"  bid #{i + 1}: {orderBook.Bids[i].Quantity} {symbolPair.BaseSymbol} @ {orderBook.Bids[i].Price} {symbolPair.QuoteSymbol}")
                .ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync("Disposing order book subscription, trade API client, and script API.").ConfigureAwait(false);
    }
}