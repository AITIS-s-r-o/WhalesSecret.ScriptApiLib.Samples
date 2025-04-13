using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Basic sample that demonstrates how an order book subscription can be created and consumed.
/// </summary>
public class OrderBookBasic : IScriptApiSample
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
        Console.WriteLine($"Create subscription for '{symbolPair}' order book on {exchangeMarket}.");
        await using IOrderBookSubscription subscription = await tradeClient.CreateOrderBookSubscriptionAsync(symbolPair).ConfigureAwait(false);

        Console.WriteLine($"Order book subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.");

        Console.WriteLine($"Wait for next order book update for {symbolPair}.");
        OrderBook orderBook = await subscription.GetOrderBookAsync(getMode: OrderBookGetMode.WaitUntilNew, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Up-to-date order book snapshot '{orderBook}' has been received.");

        Console.WriteLine();

        OrderBookHelper.PrintOrderBook(orderBook);

        Console.WriteLine("Disposing order book subscription, trade API client, and script API.");
    }
}