using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Updates;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample that demonstrates how to consume order updates. The sample creates 2 limit orders and then cancels them. The updates are consumed after the first order is created to
/// demonstrate the discovery of an open order.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class OrderUpdates : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        // In order to unlock large orders, a valid license has to be used.
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
        decimal orderSize = Math.Round(exchangeOrderSize / limitPrice, decimals: helper.VolumePrecision);

        await Console.Out.WriteLineAsync("Creating the first limit order.").ConfigureAwait(false);
        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"updates-sample-1-{DateTime.UtcNow.Ticks}");
        ILiveLimitOrder liveOrder = await tradeClient.CreateLimitOrderAsync(clientOrderId, symbolPair, OrderSide.Buy, price: limitPrice, size: orderSize, timeoutCts.Token)
            .ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Limit order '{liveOrder}' is live now.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Start background task that consumes order's updates.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        using CancellationTokenSource ordersUpdatesCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
        Task ordersUpdatesTask = Task.Run(async () =>
        {
            await Console.Out.WriteLineAsync("[UPD] Background task started.").ConfigureAwait(false);

            try
            {
                await foreach (IOrdersUpdate update in tradeClient.GetOrdersUpdateAsync(ordersUpdatesCts.Token).ConfigureAwait(false))
                {
                    await Console.Out.WriteLineAsync($$"""
                        
                        [UPD] Order update received: {{update}}
                        
                        """).ConfigureAwait(false);
                        
                }
            }
            catch (OperationCanceledException)
            {
                await Console.Out.WriteLineAsync("[UPD] Background task cancelled.").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync("[UPD] Background task ended.").ConfigureAwait(false);
        }, timeoutCts.Token);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync("Creating the second limit order.").ConfigureAwait(false);
        clientOrderId = string.Create(CultureInfo.InvariantCulture, $"updates-sample-2-{DateTime.UtcNow.Ticks}");
        ILiveLimitOrder liveOrder2 = await tradeClient.CreateLimitOrderAsync(clientOrderId, symbolPair, OrderSide.Buy, price: limitPrice, size: orderSize, timeoutCts.Token)
            .ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Second limit order '{liveOrder}' is live now.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Cancel all orders.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await tradeClient.CancelAllOrdersAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Send cancellation to the background task.").ConfigureAwait(false);
        await ordersUpdatesCts.CancelAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait for the background task to finish.").ConfigureAwait(false);
        await ordersUpdatesTask.ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }
}