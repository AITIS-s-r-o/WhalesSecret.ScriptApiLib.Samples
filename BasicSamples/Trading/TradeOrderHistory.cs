using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.SharedLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.API.TradingV1.Trades;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how historical records of trades and orders.
/// <para>This sample uses <see cref="OrderRequestBuilder{TOrderRequest}"/>. See <see cref="RequestBuilder"/> for more information about it.</para>
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>
/// Note that for <see cref="ExchangeMarket.BinanceSpot"/> the exchange account initialization may take several minutes. The initialization is done only once, so only the first
/// run of the sample is affected. Due to the nature of its API, this initialization is required for the historical records to be obtained correctly.
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class TradeOrderHistory : IScriptApiSample
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

        OrderRequestBuilder<LimitOrderRequest> limitBuilder = new(helper.ExchangeInfo);

        string clientOrderId = "history-sample-1";
        string clientOrderId2 = "history-sample-2";

        if (exchangeMarket == ExchangeMarket.BinanceSpot)
        {
            Console.WriteLine($"WARNING: When this sample is run for the first time for {exchangeMarket}, exchange account initialization needs to be done. This"
                + " operation may take several minutes to complete.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        IReadOnlyList<IOrder> orders = await tradeClient.GetOrdersAsync(today, OrderFilterOptions.AllOrders, timeoutCts.Token).ConfigureAwait(false);
        if (orders.Count == 0)
        {
            Console.WriteLine("No orders found for today, place 2 market orders.");

            decimal quoteOrderSize = exchangeMarket switch
            {
                ExchangeMarket.BinanceSpot => 6.0m,
                ExchangeMarket.KucoinSpot => 1.0m,
                _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
            };

            Console.WriteLine("Build market order request 1.");
            OrderRequestBuilder<MarketOrderRequest> marketBuilder = limitBuilder.ConvertTo<MarketOrderRequest>();
            MarketOrderRequest marketOrderRequest1 = marketBuilder
                .SetClientOrderId(clientOrderId)
                .SetSide(OrderSide.Buy)
                .SetSymbolPair(symbolPair)
                .SetClientOrderId(clientOrderId2)
                .SetSizeInBaseSymbol(false)
                .SetSize(quoteOrderSize)
                .Build();

            Console.WriteLine($"Constructed market order request 1: {marketOrderRequest1}");

            Console.WriteLine("Build market order request 2.");
            MarketOrderRequest marketOrderRequest2 = marketBuilder
                .SetClientOrderId(clientOrderId2)
                .SetSide(OrderSide.Sell)
                .Build();

            Console.WriteLine($"Constructed market order request 2: {marketOrderRequest2}");

            Console.WriteLine("Place both orders.");
            Console.WriteLine();

            ILiveMarketOrder marketOrder1 = await tradeClient.CreateOrderAsync(marketOrderRequest1, timeoutCts.Token).ConfigureAwait(false);
            ILiveMarketOrder marketOrder2 = await tradeClient.CreateOrderAsync(marketOrderRequest2, timeoutCts.Token).ConfigureAwait(false);

            Console.WriteLine($"Market order '{marketOrder1} is live.");
            Console.WriteLine($"Market order '{marketOrder2} is live.");
            Console.WriteLine();

            Console.WriteLine("Wait until the market order 1 is filled.");
            await marketOrder1.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

            Console.WriteLine("Wait until the market order 2 is filled.");
            await marketOrder2.WaitForFillAsync(timeoutCts.Token).ConfigureAwait(false);

            orders = await tradeClient.GetOrdersAsync(today, OrderFilterOptions.AllOrders, timeoutCts.Token).ConfigureAwait(false);
        }

        IReadOnlyList<ITrade> trades = await tradeClient.GetTradesAsync(today, TradeFilterOptions.AllTrades, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Following orders were found for {today}:");
        for (int i = 0; i < orders.Count; i++)
            Console.WriteLine($"  #{i + 1}: {orders[i]}");

        Console.WriteLine();
        Console.WriteLine($"Following trades were found for {today}:");
        for (int i = 0; i < trades.Count; i++)
            Console.WriteLine($"  #{i + 1}: {trades[i]}");

        Console.WriteLine();

        Console.WriteLine("Disposing trade API client and script API.");
    }
}