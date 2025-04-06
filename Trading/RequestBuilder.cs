using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Exchanges;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample that demonstrates how to use <see cref="OrderRequestBuilder{TOrderRequest}"/> to build orders. The sample creates and places a buy limit order and then it creates
/// and places two market orders (buy and sell). The limit order is then cancelled.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>
/// The order request builder uses <see cref="ExchangeInfo">exchange information</see> in order to prevent us submitting invalid order requests. For example, using the order
/// request builder, it is not possible to place order for an unsupported symbol pair. Similarly, the builder takes care of necessary rounding. Each exchange imposes different rules
/// on each tradable symbol pair and the order request builder makes it easy to comply with these requirements. For example, we can ask the order request builder to create an order
/// to buy <c>5.123456</c> USD worth of BTC, but if the volume precision of the given symbol pair on the given exchange market is limited to 3 decimal places, when we build
/// the order request, it will round the volume to <c>5.123</c> USD. At the same time, the original requested size is preserved, so if the order builder is then used to buy
/// different asset, e.g. trading LTC/USD, if the volume precision there is 4 decimal places, the order request will have size set to <c>5.1235</c> USD worth of BTC.
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class RequestBuilder : IScriptApiSample
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

        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"builder-sample-1-{DateTime.UtcNow.Ticks}");
        string clientOrderId2 = string.Create(CultureInfo.InvariantCulture, $"builder-sample-2-{DateTime.UtcNow.Ticks}");
        string clientOrderId3 = string.Create(CultureInfo.InvariantCulture, $"builder-sample-3-{DateTime.UtcNow.Ticks}");

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

        // The builder remembers all the settings used for the limit order and these settings are preserved, if possible, even when we convert the builder to a builder for
        // a different type of order. Therefore, we do not need to set the order side or the symbol pair again. We would not need to set the size either, but we want to demonstrate
        // here that market orders can be placed with size specified in quote symbol (i.e. we can request buying 5 USD worth of BTC, instead of requesting buying 0.0000xxxx BTC.
        await Console.Out.WriteLineAsync("Build market order request 1.").ConfigureAwait(false);
        OrderRequestBuilder<MarketOrderRequest> marketBuilder = limitBuilder.ConvertTo<MarketOrderRequest>();
        MarketOrderRequest marketOrderRequest1 = marketBuilder
            .SetClientOrderId(clientOrderId2)
            .SetSizeInBaseSymbol(false)
            .SetSize(quoteOrderSize)
            .Build();

        await Console.Out.WriteLineAsync($"Constructed market order request 1: {marketOrderRequest1}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Build market order request 2.").ConfigureAwait(false);
        MarketOrderRequest marketOrderRequest2 = marketBuilder
            .SetClientOrderId(clientOrderId3)
            .SetSide(OrderSide.Sell)
            .Build();

        await Console.Out.WriteLineAsync($"Constructed market order request 2: {marketOrderRequest2}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Place the three orders.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        ILiveLimitOrder limitOrder = await tradeClient.CreateOrderAsync(limitOrderRequest, timeoutCts.Token).ConfigureAwait(false);
        ILiveMarketOrder marketOrder1 = await tradeClient.CreateOrderAsync(marketOrderRequest1, timeoutCts.Token).ConfigureAwait(false);
        ILiveMarketOrder marketOrder2 = await tradeClient.CreateOrderAsync(marketOrderRequest2, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Limit order '{limitOrder}' is live.").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Market order '{marketOrder1} is live.");
        await Console.Out.WriteLineAsync($"Market order '{marketOrder2} is live.");
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Cancel the limit order.");
        await tradeClient.CancelOrderAsync(limitOrder, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait until the limit order is closed.").ConfigureAwait(false);
        _ = await limitOrder.WaitUntilClosedAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait until the market order 1 is filled.").ConfigureAwait(false);
        await marketOrder1.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait until the market order 2 is filled.").ConfigureAwait(false);
        await marketOrder2.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }
}