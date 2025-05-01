using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.AccessLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to cancel all orders.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class CancelAllOrders : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(10));

        // In order to unlock large orders, a valid license has to be used.
        CreateOptions createOptions = new(license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, timeoutCts.Token).ConfigureAwait(false);

        await using OrderSampleHelper helper = await OrderSampleHelper.InitializeAsync(scriptApi, exchangeMarket, timeoutCts.Token).ConfigureAwait(false);
        ITradeApiClient tradeClient = helper.TradeApiClient;
        SymbolPair symbolPair = helper.SelectedSymbolPair;

        Console.WriteLine("List open orders.");
        IReadOnlyList<ILiveOrder> liveOrders = await tradeClient.GetOpenOrdersAsync(OrderFilterOptions.AllOrders, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine();

        if (liveOrders.Count > 0)
        {
            Console.WriteLine("Following open orders were found:");
            for (int i = 0; i < liveOrders.Count; i++)
                Console.WriteLine($"  #{i + 1}: {liveOrders[i]}");

            Console.WriteLine("Cancel all orders.");
            await tradeClient.CancelAllOrdersAsync(timeoutCts.Token).ConfigureAwait(false);

            Console.WriteLine("List open orders again.");
            liveOrders = await tradeClient.GetOpenOrdersAsync(OrderFilterOptions.AllOrders, timeoutCts.Token).ConfigureAwait(false);

            Console.WriteLine();

            if (liveOrders.Count > 0)
            {
                Console.WriteLine("Following open orders were found:");
                for (int i = 0; i < liveOrders.Count; i++)
                    Console.WriteLine($"  #{i + 1}: {liveOrders[i]}");
            }
            else Console.WriteLine("No open orders were found.");

            Console.WriteLine();
        }
        else Console.WriteLine("No open orders were found.");

        Console.WriteLine("Disposing trade API client and script API.");
    }
}