using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets.Updates;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;
using static WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets.IBracketedOrdersFactory;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample that demonstrates how to use bracketed orders.
/// <para>
/// Bracketed order is a synthetic order type that opens a trading position with a so called working order, and then attempts to close the position with, so called, bracket orders
/// that either lock a profit or a loss. Bracketed order also includes a closing order request that is executed when the position is to be closed "manually", i.e. not relying on
/// the bracket orders. The closing order is a market order with opposite side to the working order.
/// </para>
/// <para>
/// Schema of a bracketed order with a buy limit working order a single take-profit bracket order and a single stop-loss bracket order is as follows:
///
///                                                             ┌────────────────────────┐
///                                                             │ Sell take-profit order │
///                                                  ┌──────►   │         $80,500        │
///                                                  │          └────────────────────────┘
///                                                  │                (Locks profit)
///   ┌─────────────────┐                            │
///   │ Buy limit order │     If filled, place       │
///   │     $80,000     │────────────────────────────┼
///   └─────────────────┘      bracket orders        │
///     (Working order)                              │
///                                                  │          ┌────────────────────────┐
///                                                  │          │  Sell stop-loss order  │
///                                                  └──────►   │         $79,600        │
///                                                             └────────────────────────┘
///                                                                    (Locks loss).
/// </para>
/// <para>
/// A single bracketed order may have up to <see cref="IBracketOrdersManager.MaxBracketOrders"/> bracket orders. The number of stop-loss orders does not need to match the number of
/// take-profit orders. Nor do their sizes need to match. However, the sum of the sizes on each side must not exceed the size of the working order.
/// </para>
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class BracketedOrder : IScriptApiSample
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

        OrderRequestBuilder<MarketOrderRequest> marketBuilder = new(helper.ExchangeInfo);

        string clientOrderId = "brackord";

        // Buy a small amount of bitcoin.
        decimal quoteOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 20.0m,
            ExchangeMarket.KucoinSpot => 5.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        Console.WriteLine();
        Console.WriteLine("Build a market order request for the first order.");
        MarketOrderRequest workingOrderRequest = marketBuilder
            .SetClientOrderId(clientOrderId)
            .SetSide(OrderSide.Buy)
            .SetSymbolPair(symbolPair)
            .SetSizeInBaseSymbol(false)
            .SetSize(quoteOrderSize)
            .Build();

        // We will place market buy order and put 50% stop-loss to be at 100 EUR (or USDT) below the last best ask price (roughly the price we expect to buy for) and a second
        // stop-loss, 50% again, at 200 EUR below the best ask price. We also put a 30% take-profit to be at 200 EUR (or USDT) above the last best ask and 70% take-profit at
        // 400 EUR above the best ask price.
        decimal stopLossPrice2 = helper.BestAsk - 200;
        decimal stopLossPrice1 = helper.BestAsk - 100;
        decimal takeProfitPrice1 = helper.BestAsk + 200;
        decimal takeProfitPrice2 = helper.BestAsk + 400;

        BracketOrderDefinition[] bracketOrdersDefinitions = new BracketOrderDefinition[]
        {
             new(BracketOrderType.StopLoss, thresholdPrice: stopLossPrice2, 50m),
             new(BracketOrderType.StopLoss, thresholdPrice: stopLossPrice1, 50m),
             new(BracketOrderType.TakeProfit, thresholdPrice: takeProfitPrice1, 30m),
             new(BracketOrderType.TakeProfit, thresholdPrice: takeProfitPrice2, 70m),
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

            BracketedOrderStatus state = liveBracketedOrder.Status;
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