using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using WhalesSecret.ScriptApiLib.Samples.AccessLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Updates;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to consume order updates. The sample creates 2 limit orders and then cancels them. The updates are consumed after the first order is created to
/// demonstrate the discovery of an open order.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>IMPORTANT: You have to change the keys and the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class OrderUpdates : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        CreateOptions createOptions = new(license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, timeoutCts.Token).ConfigureAwait(false);

        await using OrderSampleHelper helper = await OrderSampleHelper.InitializeAsync(scriptApi, exchangeMarket, timeoutCts.Token).ConfigureAwait(false);
        ITradeApiClient tradeClient = helper.TradeApiClient;
        SymbolPair symbolPair = helper.SelectedSymbolPair;

        // Compute a limit price so that the order is unlikely to fill.
        decimal limitPrice = Math.Floor(helper.BestBid / 5 * 4);

        // Buy a small amount of bitcoin.
        decimal exchangeOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 6.0m,
            ExchangeMarket.KucoinSpot => 2.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        // Rounding is necessary to get accepted on exchanges.
        decimal orderSize = Math.Round(exchangeOrderSize / limitPrice, decimals: helper.BaseVolumePrecision);

        Console.WriteLine("Creating the first limit order.");
        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"updates-sample-1-{DateTime.UtcNow.Ticks}");
        ILiveLimitOrder liveOrder = await tradeClient.CreateLimitOrderAsync(clientOrderId, symbolPair, OrderSide.Buy, price: limitPrice, size: orderSize, timeoutCts.Token)
            .ConfigureAwait(false);

        Console.WriteLine($"Limit order '{liveOrder}' is live now.");
        Console.WriteLine();

        Console.WriteLine("Start background task that consumes order's updates.");
        Console.WriteLine();

        using CancellationTokenSource ordersUpdatesCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
        Task ordersUpdatesTask = Task.Run(async () =>
        {
            Console.WriteLine("[UPD] Background task started.");

            try
            {
                await foreach (IOrdersUpdate update in tradeClient.GetOrdersUpdateAsync(ordersUpdatesCts.Token).ConfigureAwait(false))
                {
                    Console.WriteLine($$"""
                        
                        [UPD] Order update received: {{update}}
                        
                        """);
                        
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[UPD] Background task cancelled.");
            }

            Console.WriteLine("[UPD] Background task ended.");
        }, timeoutCts.Token);

        Console.WriteLine();
        Console.WriteLine("Creating the second limit order.");
        clientOrderId = string.Create(CultureInfo.InvariantCulture, $"updates-sample-2-{DateTime.UtcNow.Ticks}");
        ILiveLimitOrder liveOrder2 = await tradeClient.CreateLimitOrderAsync(clientOrderId, symbolPair, OrderSide.Buy, price: limitPrice, size: orderSize, timeoutCts.Token)
            .ConfigureAwait(false);

        Console.WriteLine($"Second limit order '{liveOrder}' is live now.");
        Console.WriteLine();

        Console.WriteLine("Cancel all orders.");
        Console.WriteLine();

        await tradeClient.CancelAllOrdersAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine("Send cancellation to the background task.");
        await ordersUpdatesCts.CancelAsync().ConfigureAwait(false);

        Console.WriteLine("Wait for the background task to finish.");
        await ordersUpdatesTask.ConfigureAwait(false);

        Console.WriteLine("Disposing trade API client and script API.");
    }
}