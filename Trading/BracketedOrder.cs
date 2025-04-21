using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;

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

        await using OrderSampleHelper helper = await OrderSampleHelper.InitializeAsync(scriptApi, exchangeMarket, timeoutCts.Token).ConfigureAwait(false);
        ITradeApiClient tradeClient = helper.TradeApiClient;
        SymbolPair symbolPair = helper.SelectedSymbolPair;

        OrderRequestBuilder<MarketOrderRequest> marketBuilder = new(helper.ExchangeInfo);

        // The client order ID suffix is a special requirement when the budget is used. We can either use null client order ID in our requests, or we need to specify the suffix,
        // which will be altered by the budget. The suffix is used to uniquely identify the orders that are created by trade API client with the budget.
        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"budget-sample-1{ITradingStrategyBudget.ClientOrderIdSuffix}");
        string clientOrderId2 = string.Create(CultureInfo.InvariantCulture, $"budget-sample-2{ITradingStrategyBudget.ClientOrderIdSuffix}");

        // Buy a small amount of bitcoin.
        decimal quoteOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 6.0m,
            ExchangeMarket.KucoinSpot => 1.0m,
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

        decimal stopLossPrice = helper.BestAsk - 200;
        decimal takeProfitPrice = helper.BestAsk + 200;

        // We will place market buy order and put a stop-loss to be at 200 EUR (or USDT) below the last best ask price (roughly the price we expect to buy for) and we also put
        // a take-profit to be at 200 EUR (or USDT) above the last best ask.
        BracketOrderDefinition[] bracketOrdersDefinitions = new BracketOrderDefinition[]
        {
             new(BracketOrderType.StopLoss, thresholdPrice: stopLossPrice, workingOrderRequest.Size),
             new(BracketOrderType.TakeProfit, thresholdPrice: takeProfitPrice, workingOrderRequest.Size),
        };

        await using ILiveBracketedOrder liveBracketedOrder = await tradeClient.CreateBracketedOrderAsync(workingOrderRequest, bracketOrdersDefinitions, timeoutCts.Token)
            .ConfigureAwait(false);

        Console.WriteLine($"Working market order request '{workingOrderRequest}' has been placed into the live bracketed order '{liveBracketedOrder}'.");
        Console.WriteLine();
        Console.WriteLine("The program will terminate when the bracketed order is terminated.");
        Console.WriteLine("Press Ctrl+C to close the position manually and terminate at any time.");
        Console.WriteLine();

        using CancellationTokenSource terminateTokenSource = new();

        // Install Ctrl+C / SIGINT handler.
        ConsoleCancelEventHandler controlCancelHandler = (object? sender, ConsoleCancelEventArgs e) =>
        {
            // If cancellation of the control event is set to true, the process won't terminate automatically and we will have control over the shutdown.
            e.Cancel = true;

            terminateTokenSource.Cancel();
        };

        Console.CancelKeyPress += controlCancelHandler;

        try
        {
            await liveBracketedOrder.TerminatedEvent.WaitAsync(terminateTokenSource.Token).ConfigureAwait(false);
            Console.WriteLine($"Live bracketed order '{liveBracketedOrder}' has been terminated.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Break detected, closing position.");
            await liveBracketedOrder.ClosePositionAsync().ConfigureAwait(false);

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