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
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample that demonstrates how to use <see cref="OrderRequestBuilder{TOrderRequest}"/> to build orders. The sample creates and places a buy limit order and then it creates
/// and places 2 market orders (buy and sell). THe limit order is then cancelled.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class RequestBuilder : IScriptApiSample
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

        OrderRequestBuilder<LimitOrderRequest> limitBuilder = new();

        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"limitBuilder-sample-1-{DateTime.UtcNow.Ticks}");
        string clientOrderId2 = string.Create(CultureInfo.InvariantCulture, $"limitBuilder-sample-2-{DateTime.UtcNow.Ticks}");
        string clientOrderId3 = string.Create(CultureInfo.InvariantCulture, $"limitBuilder-sample-3-{DateTime.UtcNow.Ticks}");

        // Buy a small amount of bitcoin.
        decimal baseOrderSize = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => 6.0m,
            ExchangeMarket.KucoinSpot => 2.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        // Compute a limit price so that the order is unlikely to fill.
        decimal limitPrice = Math.Floor(helper.BestBid / 5 * 4);

        // Rounding is necessary to get accepted on exchanges.
        decimal orderSize = Math.Round(baseOrderSize / limitPrice, decimals: helper.VolumePrecision);

        await Console.Out.WriteLineAsync("Build limit order request.").ConfigureAwait(false);
        LimitOrderRequest limitOrderRequest = limitBuilder
            .SetClientOrderId(clientOrderId)
            .SetSide(OrderSide.Buy)
            .SetSymbolPair(symbolPair)
            .SetSizeInBaseSymbol(true)
            .SetSize(orderSize)
            .SetPrice(limitPrice)
            .Build();

        await Console.Out.WriteLineAsync($"Constructed limit order request: {limitOrderRequest}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Build market order request 1.").ConfigureAwait(false);
        OrderRequestBuilder<MarketOrderRequest> marketBuilder = limitBuilder.ConvertTo<MarketOrderRequest>();
        MarketOrderRequest marketOrderRequest1 = marketBuilder
            .SetClientOrderId(clientOrderId2)
            .SetSizeInBaseSymbol(false)
            .SetSize(baseOrderSize)
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

        await Console.Out.WriteLineAsync($"Limit order '{limitOrder}', market order '{marketOrder1}' and '{marketOrder2}' are live now. Cancel the limit order.")
            .ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        await tradeClient.CancelOrderAsync(limitOrder, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait until the limit order is closed.").ConfigureAwait(false);
        await limitOrder.WaitUntilClosedAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait until the market order 1 is filled.").ConfigureAwait(false);
        await marketOrder1.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Wait until the market order 2 is filled.").ConfigureAwait(false);
        await marketOrder2.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }
}