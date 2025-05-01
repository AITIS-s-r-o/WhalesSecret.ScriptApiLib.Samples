using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.AccessLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to get a list of open orders.
/// <para>This sample uses <see cref="OrderRequestBuilder{TOrderRequest}"/>. See <see cref="RequestBuilder"/> for more information about it.</para>
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>
/// IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.
/// </remarks>
public class ListOpenOrders : IScriptApiSample
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

        OrderRequestBuilder<LimitOrderRequest> limitBuilder = new(helper.ExchangeInfo);

        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"activeOrders-1-{DateTime.UtcNow.Ticks}");

        // Buy a small amount of bitcoin.
        decimal quoteOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 6.0m,
            ExchangeMarket.KucoinSpot => 1.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        // Compute a limit price so that the order is unlikely to fill.
        decimal limitPrice = Math.Floor(helper.BestBid / 5 * 4);

        // When using the order request builder, we do not need to round sizes and prices. The builder takes care of these requirements as well as other things.
        decimal orderSize = quoteOrderSize / limitPrice;

        Console.WriteLine("Build a limit order request.");
        LimitOrderRequest limitOrderRequest = limitBuilder
            .SetClientOrderId(clientOrderId)
            .SetSide(OrderSide.Buy)
            .SetSymbolPair(symbolPair)
            .SetSizeInBaseSymbol(true)
            .SetSize(orderSize)
            .SetPrice(limitPrice)
            .Build();

        Console.WriteLine($"Constructed limit order request: {limitOrderRequest}");

        Console.WriteLine("Place the order.");
        Console.WriteLine();

        ILiveLimitOrder limitOrder = await tradeClient.CreateOrderAsync(limitOrderRequest, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Limit order '{limitOrder}' is live.");

        Console.WriteLine("List open orders.");
        IReadOnlyList<ILiveOrder> liveOrders = await tradeClient.GetOpenOrdersAsync(OrderFilterOptions.AllOrders, timeoutCts.Token).ConfigureAwait(false);
        
        Console.WriteLine();
        Console.WriteLine("Following open orders were found:");
        for (int i = 0; i < liveOrders.Count; i++)
            Console.WriteLine($"  #{i + 1}: {liveOrders[i]}");

        Console.WriteLine();

        Console.WriteLine("Cancel the order.");
        await tradeClient.CancelOrderAsync(limitOrder, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine();

        Console.WriteLine("Disposing trade API client and script API.");
    }
}