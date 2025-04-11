using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Basic sample that demonstrates how a candlestick subscription can be created and consumed.
/// </summary>
public class CandleBasic : IScriptApiSample
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
        Console.WriteLine($"Create subscription for '{symbolPair}' candlesticks on {exchangeMarket}.");
        await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(symbolPair).ConfigureAwait(false);

        Console.WriteLine($"Candlestick subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.");

        CandleWidth candleWidth = CandleWidth.Minute1;
        Console.WriteLine($"Wait for next candle update for candle width {candleWidth}.");
        CandleUpdate candleUpdate = await subscription.WaitNextCandlestickUpdateAsync(candleWidth, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Candle update '{candleUpdate}' has been received.");

        Console.WriteLine("Disposing candlestick subscription, trade API client, and script API.");
    }
}