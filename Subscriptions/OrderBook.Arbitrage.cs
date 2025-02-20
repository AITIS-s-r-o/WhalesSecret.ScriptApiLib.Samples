using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Exchanges;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Advanced sample that demonstrates how to monitor order books on two different exchanges at the same time.
/// </summary>
public class OrderBookArbitrage : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync().ConfigureAwait(false);

        ExchangeMarket primaryMarket = exchangeMarket;
        ExchangeMarket secondaryMarket = exchangeMarket == ExchangeMarket.BinanceSpot ? ExchangeMarket.KucoinSpot : ExchangeMarket.BinanceSpot;

        await Console.Out.WriteLineAsync($"Initialize exchange market {primaryMarket} and {secondaryMarket}.").ConfigureAwait(false);
        Task<ExchangeInfo>[] initTasks = new Task<ExchangeInfo>[2];

        initTasks[0] = scriptApi.InitializeMarketAsync(primaryMarket, timeoutCts.Token);
        initTasks[1] = scriptApi.InitializeMarketAsync(secondaryMarket, timeoutCts.Token);

        _ = await Task.WhenAll(initTasks).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Connect to {primaryMarket} and {secondaryMarket} exchanges with public connections.").ConfigureAwait(false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);

        Task<ITradeApiClient>[] connectTasks = new Task<ITradeApiClient>[2];

        connectTasks[0] = scriptApi.ConnectAsync(primaryMarket, connectionOptions);
        connectTasks[1] = scriptApi.ConnectAsync(secondaryMarket, connectionOptions);

        _ = await Task.WhenAll(connectTasks).ConfigureAwait(false);

        await using ITradeApiClient primaryTradeApiClient = await connectTasks[0].ConfigureAwait(false);
        await using ITradeApiClient secondaryTradeApiClient = await connectTasks[1].ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Public connections to {primaryMarket} and {secondaryMarket} have been established successfully.").ConfigureAwait(false);

        SymbolPair symbolPair = SymbolPair.BTC_USDT;

        await Console.Out.WriteLineAsync($"Create order book subscriptions for symbol pair '{symbolPair}' on {primaryMarket} and {secondaryMarket}.").ConfigureAwait(false);

        Task<IOrderBookSubscription>[] subscriptionTasks = new Task<IOrderBookSubscription>[2];

        subscriptionTasks[0] = primaryTradeApiClient.CreateOrderBookSubscriptionAsync(symbolPair);
        subscriptionTasks[1] = secondaryTradeApiClient.CreateOrderBookSubscriptionAsync(symbolPair);

        await Console.Out.WriteLineAsync($"'{symbolPair}' order book subscriptions on {primaryMarket} and {secondaryMarket} have been created successfully.").ConfigureAwait(false);

        IOrderBookSubscription primarySubscription = await subscriptionTasks[0].ConfigureAwait(false);
        IOrderBookSubscription secondarySubscription = await subscriptionTasks[1].ConfigureAwait(false);

        OrderBook? primaryOrderBook = null;
        OrderBook? secondaryOrderBook = null;
        Task<OrderBook>? primaryOrderBookTask = null;
        Task<OrderBook>? secondaryOrderBookTask = null;

        decimal? bestProfit = null;
        string? bestOpportunity = null;
        string lastOpportunity1 = string.Empty;
        string lastOpportunity2 = string.Empty;
        int counter = 0;
        while (true)
        {
            try
            {
                if (primaryOrderBookTask is null)
                    primaryOrderBookTask = primarySubscription.GetOrderBookAsync(getMode: OrderBookGetMode.WaitUntilNew, timeoutCts.Token);

                if (secondaryOrderBookTask is null)
                    secondaryOrderBookTask = secondarySubscription.GetOrderBookAsync(getMode: OrderBookGetMode.WaitUntilNew, timeoutCts.Token);

                _ = await Task.WhenAny(primaryOrderBookTask, secondaryOrderBookTask).ConfigureAwait(false);

                if (primaryOrderBookTask.IsCompleted)
                {
                    primaryOrderBook = await primaryOrderBookTask.ConfigureAwait(false);
                    primaryOrderBookTask = null;
                }

                if (secondaryOrderBookTask.IsCompleted)
                {
                    secondaryOrderBook = await secondaryOrderBookTask.ConfigureAwait(false);
                    secondaryOrderBookTask = null;
                }

                if ((primaryOrderBook is not null) && (secondaryOrderBook is not null))
                {
                    if ((primaryOrderBook.Value.Bids.Count > 0) && (secondaryOrderBook.Value.Asks.Count > 0))
                    {
                        OrderBookEntry primaryBestBid = primaryOrderBook.Value.Bids[0];
                        OrderBookEntry secondaryBestAsk = primaryOrderBook.Value.Asks[1];

                        decimal amount = Math.Min(primaryBestBid.Quantity, secondaryBestAsk.Quantity);
                        decimal priceDiff = primaryBestBid.Price - secondaryBestAsk.Price;
                        decimal profit = amount * priceDiff;
                        string opportunity = $"Opportunity: {amount} {symbolPair.BaseSymbol} with {profit} {symbolPair.QuoteSymbol} profit on {
                            priceDiff} price difference (not counting fees)";

                        if (opportunity != lastOpportunity1)
                        {
                            lastOpportunity1 = opportunity;

                            if ((bestProfit is null) || (profit > bestProfit))
                            {
                                bestProfit = profit;
                                bestOpportunity = opportunity;
                            }

                            await Console.Out.WriteLineAsync($"{primaryMarket} best bid: {primaryBestBid.Quantity} {symbolPair.BaseSymbol} @ {primaryBestBid.Price} {
                                symbolPair.QuoteSymbol}").ConfigureAwait(false);

                            await Console.Out.WriteLineAsync($"{secondaryMarket} best ask: {secondaryBestAsk.Quantity} {symbolPair.BaseSymbol} @ {secondaryBestAsk.Price} {
                                symbolPair.QuoteSymbol}").ConfigureAwait(false);

                            await Console.Out.WriteLineAsync(opportunity).ConfigureAwait(false);
                            await Console.Out.WriteLineAsync().ConfigureAwait(false);
                        }
                    }

                    if ((primaryOrderBook.Value.Asks.Count > 0) && (secondaryOrderBook.Value.Bids.Count > 0))
                    {
                        OrderBookEntry secondaryBestBid = primaryOrderBook.Value.Bids[1];
                        OrderBookEntry primaryBestAsk = primaryOrderBook.Value.Asks[0];

                        decimal amount = Math.Min(primaryBestAsk.Quantity, secondaryBestBid.Quantity);
                        decimal priceDiff = secondaryBestBid.Price - primaryBestAsk.Price;
                        decimal profit = amount * priceDiff;
                        string opportunity = $"Opportunity: {amount} {symbolPair.BaseSymbol} with {profit} {symbolPair.QuoteSymbol} profit on {
                            priceDiff} price difference (not counting fees)";

                        if (opportunity != lastOpportunity2)
                        {
                            lastOpportunity2 = opportunity;

                            if ((bestProfit is null) || (profit > bestProfit))
                            {
                                bestProfit = profit;
                                bestOpportunity = opportunity;
                            }

                            await Console.Out.WriteLineAsync($"{secondaryMarket} best bid: {secondaryBestBid.Quantity} {symbolPair.BaseSymbol} @ {secondaryBestBid.Price} {
                                symbolPair.QuoteSymbol}").ConfigureAwait(false);

                            await Console.Out.WriteLineAsync($"{primaryMarket} best ask: {primaryBestAsk.Quantity} {symbolPair.BaseSymbol} @ {primaryBestAsk.Price} {
                                symbolPair.QuoteSymbol}").ConfigureAwait(false);

                            await Console.Out.WriteLineAsync(opportunity).ConfigureAwait(false);
                            await Console.Out.WriteLineAsync().ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if ((counter % 100) == 0)
            {
                await Console.Out.WriteLineAsync($"After {counter} iterations, best opportunity we've seen so far was:").ConfigureAwait(false);
                await Console.Out.WriteLineAsync(bestOpportunity).ConfigureAwait(false);
                await Console.Out.WriteLineAsync().ConfigureAwait(false);
            }

            counter++;
        }

        await Console.Out.WriteLineAsync("Disposing both order book subscription subsets, trade API client, and script API.").ConfigureAwait(false);
    }
}