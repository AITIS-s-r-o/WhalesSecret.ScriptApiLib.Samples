using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.SharedLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets.Updates;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;
using static WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets.IBracketedOrdersFactory;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to use bracketed orders with a trailing stop-loss.
/// </summary>
/// <seealso cref="ILiveBracketedOrder"/>
/// <seealso cref="BracketedOrder"/>
/// <seealso cref="BracketOrderType.TrailingStopLoss"/>
/// <remarks>
/// Trailing stop-loss bracket order is placed below buy orders, or above sell orders, in order to cap the maximum loss, just like the regular
/// <see cref="BracketOrderType.StopLoss"/>. However, a trailing stop-loss order is adjusted automatically as the price moves in the direction of the trade, so it can be used to
/// lock in profits while still allowing the price to move further in the direction of the trade, thus potentially increasing the profit.
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class TrailingStopLoss : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        CreateOptions createOptions = new(license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, timeoutCts.Token).ConfigureAwait(false);

        string secondaryAsset = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => "EUR",
            ExchangeMarket.KucoinSpot => "USDT",
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        ConnectionOptions connectionOptions = new(BlockUntilReconnectedOrTimeout.InfinityTimeoutInstance, ConnectionType.FullTrading);
        await using OrderSampleHelper helper = await OrderSampleHelper.InitializeAsync(scriptApi, exchangeMarket, timeoutCts.Token, connectionOptions).ConfigureAwait(false);
        ITradeApiClient tradeClient = helper.TradeApiClient;
        SymbolPair symbolPair = helper.SelectedSymbolPair;

        string clientOrderId = "brackord";

        // Buy a small amount of bitcoin.
        decimal quoteOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 20.0m,
            ExchangeMarket.KucoinSpot => 5.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        Console.WriteLine();
        Console.WriteLine("Build a market order request for the working order to enter your trading position.");
        MarketOrderRequest workingOrderRequest = new OrderRequestBuilder<MarketOrderRequest>(helper.ExchangeInfo)
            .SetClientOrderId(clientOrderId)
            .SetSide(OrderSide.Buy)
            .SetSymbolPair(symbolPair)
            .SetSizeInBaseSymbol(false)
            .SetSize(quoteOrderSize)
            .Build();

        // We will place a market buy order and put 100% trailing stop-loss to be at 1% below the last best bid price (roughly the price we expect to buy for) and a take-profit at
        // 5% above. The trailing stop-loss will maintain the set distance and will be updated as the price moves in the direction of the trade, if the price moves at least 50 USDT
        // (or EUR).
        decimal stopLossPrice = helper.BestBid * 0.99m;
        decimal distance = helper.BestBid - stopLossPrice;
        decimal delta = 50.0m;

        decimal takeProfitPrice = helper.BestBid * 1.05m;

        BracketOrderDefinition[] bracketOrdersDefinitions = new BracketOrderDefinition[]
        {
             new TrailingStopLossBracketOrderDefinition(thresholdPrice: stopLossPrice, sizePercent: 100m, priceDistance: distance, replacePriceDelta: delta),
             new(BracketOrderType.TakeProfit, thresholdPrice: takeProfitPrice, sizePercent: 100m),
        };

        OnBracketedOrderUpdateAsync onBracketedOrderUpdate = (IBracketedOrderUpdate update) =>
        {
            Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: Bracket order update: {update}");

            IReadOnlyList<FillData>? fills = update switch
            {
                WorkingOrderFill workingOrderFill => workingOrderFill.Fills,
                BracketOrderFill bracketOrderFill => bracketOrderFill.Fills,
                ClosePositionOrderFill closePositionOrderFill => closePositionOrderFill.Fills,
                _ => null,
            };

            if (fills is not null)
            {
                for (int i = 0; i < fills.Count; i++)
                    Console.WriteLine($"  Fill #{i + 1}: {fills[i]}");
            }

            Console.WriteLine();
            return Task.CompletedTask;
        };

        await using ILiveBracketedOrder liveBracketedOrder = await tradeClient.CreateBracketedOrderAsync(workingOrderRequest, bracketOrdersDefinitions, onBracketedOrderUpdate,
            timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Working market order request '{workingOrderRequest}' has been placed into the live bracketed order '{liveBracketedOrder}'.");
        Console.WriteLine();
        Console.WriteLine("The program will terminate when the bracketed order is terminated.");
        Console.WriteLine("Press Ctrl+C to close the position manually and terminate at any time.");
        Console.WriteLine();

        using CancellationTokenSource terminateTokenSource = new();

        // Install Ctrl+C / SIGINT handler.
        ConsoleCancelEventHandler controlCancelHandler = (object? sender, ConsoleCancelEventArgs e) =>
        {
            Console.WriteLine("Ctrl+C / SIGINT detected.");
            // If cancellation of the control event is set to true, the process won't terminate automatically and we will have control over the shutdown.
            e.Cancel = true;

            terminateTokenSource.Cancel();
        };

        Console.CancelKeyPress += controlCancelHandler;

        try
        {
            await liveBracketedOrder.TerminatedEvent.WaitAsync(terminateTokenSource.Token).ConfigureAwait(false);

            Console.WriteLine();

            string msg = liveBracketedOrder.StatusMessage;
            if (liveBracketedOrder.Status == BracketedOrderStatus.BracketOrdersFilled) Console.WriteLine($"Live bracketed order '{liveBracketedOrder}' has been terminated. {msg}");
            else Console.WriteLine($"Error occurred, live bracketed order '{liveBracketedOrder}' terminated in state {liveBracketedOrder.Status}. {msg}");

            Console.WriteLine();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Break detected, closing position.");
            await liveBracketedOrder.ClosePositionAsync(waitForClosePositionFill: true, CancellationToken.None).ConfigureAwait(false);

            Console.WriteLine("Position has been closed.");
            Console.WriteLine();
        }
        finally
        {
            // Uninstall Ctrl+C / SIGINT handler.
            Console.CancelKeyPress -= controlCancelHandler;
        }

        Console.WriteLine("Disposing trade API client, script API, and the bracketed order.");
    }
}