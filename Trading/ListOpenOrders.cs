using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample that demonstrates how to get a list of open orders.
/// <para>This sample uses <see cref="OrderRequestBuilder{TOrderRequest}"/>. See <see cref="RequestBuilder"/> for more information about it.</para>
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class ListOpenOrders : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        // In order to unlock large orders, a valid license has to be used.
        CreateOptions createOptions = new(license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions).ConfigureAwait(false);

        await using OrderSampleHelper helper = await OrderSampleHelper.InitializeAsync(scriptApi, exchangeMarket, timeoutCts.Token).ConfigureAwait(false);
        ITradeApiClient tradeClient = helper.TradeApiClient;
        SymbolPair symbolPair = helper.SelectedSymbolPair;

        OrderRequestBuilder<LimitOrderRequest> limitBuilder = new(helper.ExchangeInfo);

        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"activeorders-1-{DateTime.UtcNow.Ticks}");

        // Buy a small amount of bitcoin.
        decimal baseOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 6.0m,
            ExchangeMarket.KucoinSpot => 1.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        // Compute a limit price so that the order is unlikely to fill.
        decimal limitPrice = Math.Floor(helper.BestBid / 5 * 4);

        // When using the order request builder, we do not need to round sizes and prices. The builder takes care of these requirements as well as other things.
        decimal orderSize = baseOrderSize / limitPrice;

        await Console.Out.WriteLineAsync("Build a limit order request.").ConfigureAwait(false);
        LimitOrderRequest limitOrderRequest = limitBuilder
            .SetClientOrderId(clientOrderId)
            .SetSide(OrderSide.Buy)
            .SetSymbolPair(symbolPair)
            .SetSizeInBaseSymbol(true)
            .SetSize(orderSize)
            .SetPrice(limitPrice)
            .Build();

        await Console.Out.WriteLineAsync($"Constructed limit order request: {limitOrderRequest}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Place the order.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        ILiveLimitOrder limitOrder = await tradeClient.CreateOrderAsync(limitOrderRequest, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Limit order '{limitOrder}' is live.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("List open orders.");
        IReadOnlyList<ILiveOrder> liveOrders = await tradeClient.GetOpenOrdersAsync(OrderFilterOptions.AllOrders, timeoutCts.Token).ConfigureAwait(false);
        
        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync("Following open orders were found:").ConfigureAwait(false);
        for (int i = 0; i < liveOrders.Count; i++)
            await Console.Out.WriteLineAsync($"  #{i + 1}: {liveOrders[i]}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Cancel the order.");
        await tradeClient.CancelOrderAsync(limitOrder, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }
}