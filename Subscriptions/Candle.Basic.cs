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

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with a public connection.").ConfigureAwait(false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Public connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        SymbolPair symbolPair = SymbolPair.BTC_USDT;
        await Console.Out.WriteLineAsync($"Create subscription for '{symbolPair}' candlesticks on {exchangeMarket}.").ConfigureAwait(false);
        await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(symbolPair).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Candlestick subscription for '{symbolPair}' on {exchangeMarket} has been created successfully as '{subscription}'.")
            .ConfigureAwait(false);

        CandleWidth candleWidth = CandleWidth.Minute1;
        await Console.Out.WriteLineAsync($"Wait for next candle update for candle width {candleWidth}.").ConfigureAwait(false);
        CandleUpdate candleUpdate = await subscription.WaitNextCandlestickUpdateAsync(candleWidth, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Candle update '{candleUpdate}' has been received.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing candlestick subscription, trade API client, and script API.").ConfigureAwait(false);
    }
}