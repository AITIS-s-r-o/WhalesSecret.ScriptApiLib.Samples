using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Advanced sample that demonstrates how multiple candlestick subscriptions can be created and consumed at the same time using <see cref="ICandlestickSubscriptionSet"/> and its
/// batch monitoring methods. It also demonstrates a possibility to remove a subscription from the set.
/// </summary>
public class CandleSet : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(5));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        _ = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with a public connection.").ConfigureAwait(false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Public connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        SymbolPair[] symbolPairs = new SymbolPair[]
        {
            SymbolPair.BTC_USDT,
            SymbolPair.LTC_BTC,
            SymbolPair.LTC_USDT,
        };

        await Console.Out.WriteLineAsync($"Create candlestick subscriptions for {symbolPairs.Length} symbol pairs on {exchangeMarket}.").ConfigureAwait(false);
        await using ICandlestickSubscriptionSet subscriptionSet = await tradeClient.CreateCandlestickSubscriptionsAsync(symbolPairs).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"{symbolPairs.Length} candlestick subscriptions on {exchangeMarket} has been created successfully.").ConfigureAwait(false);

        CandleWidth candleWidth = CandleWidth.Minute1;

        await Console.Out.WriteLineAsync($"Start batch monitoring for candle width {candleWidth}.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(candleWidth, timeoutCts.Token))
        {
            await Console.Out.WriteLineAsync($"First we get the latest closed candles for all symbol pairs.").ConfigureAwait(false);
            for (int i = 0; i < symbolPairs.Length; i++)
            {
                // When we call this method for the first time, we have not consumed any candles on the given subscriptions yet, so we get the same results as we would get if we
                // called GetLatestClosedCandlestick. However, using WhenAnyNewClosedCandlestickAsync will cause consumption of the last state and so we will be able to wait for
                // new data below.
                CandleWithExchangeSymbolPair candle = await subscriptionSet.WhenAnyNewClosedCandlestickAsync(candleWidth).ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  {DateTime.UtcNow} | Latest known closed candle: {candle}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            await Console.Out.WriteLineAsync($"Wait for closed candlesticks updates.").ConfigureAwait(false);
            for (int i = 0; i < symbolPairs.Length; i++)
            {
                // Note that here, we are not guaranteed to get one closed candle for each of the three symbol pairs in the subscription. Some exchanges do not deliver closed
                // candlestick update until there is an actual new trade.
                CandleWithExchangeSymbolPair candle = await subscriptionSet.WhenAnyNewClosedCandlestickAsync(candleWidth).ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  {DateTime.UtcNow} | New closed candle received: {candle}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            await Console.Out.WriteLineAsync($"Dispose batch monitoring to be able to remove subscription from the set.").ConfigureAwait(false);
        }

        SymbolPair removeSymbolPair = symbolPairs[1];
        await Console.Out.WriteLineAsync($"Remove and dispose '{removeSymbolPair}' subscription from the set.").ConfigureAwait(false);

        if (!subscriptionSet.TryRemoveSubscription(removeSymbolPair, out ICandlestickSubscription? subscription))
            throw new SanityCheckException($"Subscription for '{removeSymbolPair}' was not found in the set.");

        await subscription.DisposeAsync().ConfigureAwait(false);

        await Task.Delay(5000).ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Start batch monitoring again for candle width {candleWidth}.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await using (IAsyncDisposable batchMonitoring = subscriptionSet.StartBatchMonitoring(candleWidth, timeoutCts.Token))
        {
            await Console.Out.WriteLineAsync("Wait for 10 candlesticks updates from any symbol of the two remaining symbols.").ConfigureAwait(false);
            for (int i = 0; i < 10; i++)
            {
                CandleUpdate candleUpdate = await subscriptionSet.WhenAnyNewCandlestickUpdateAsync(candleWidth).ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"  {DateTime.UtcNow} | New candle update received: {candleUpdate}").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync("Dispose batch monitoring to.").ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("Disposing candlestick subscriptions set, trade API client, and script API.").ConfigureAwait(false);
    }
}