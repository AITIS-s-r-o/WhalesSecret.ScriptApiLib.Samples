using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample that demonstrates how to create orders. The sample creates a limit buy order with the price well under the current price. It is thus not expected that the order is
/// filled. Then the order is canceled. The cancellation of the order is then awaited.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// <para>Placing small orders does not need a valid license. Placing larger orders requires a valid license to be put into <see cref="License"/>.</para>
/// </summary>
/// <remarks>IMPORTANT: You have to change the keys and the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public static class SizeSampleCore
{
    /// <summary>
    /// Run the sample.
    /// </summary>
    /// <param name="exchangeMarket">Exchange market to connect to.</param>
    /// <param name="useLargeOrder">
    /// <c>true</c> to use large order, <c>false</c> to use small order. If set to <c>true</c>, <see cref="License"/> has to changed to contain a valid license.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns
    public static async Task RunSampleAsync(ExchangeMarket exchangeMarket, bool useLargeOrder)
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
            ExchangeMarket.BinanceSpot => useLargeOrder ? 25m : 6.0m,
            ExchangeMarket.KucoinSpot => useLargeOrder ? 20m : 2.0m,
            _ => throw new SanityCheckException($"Invalid exchange market {exchangeMarket} provided."),
        };

        // Rounding is necessary to get accepted on exchanges.
        decimal orderSize = Math.Round(exchangeOrderSize / limitPrice, decimals: helper.BaseVolumePrecision);

        string clientOrderId = string.Create(CultureInfo.InvariantCulture, $"order-size-sample-{DateTime.UtcNow.Ticks}");
        ILiveLimitOrder liveOrder = await tradeClient.CreateLimitOrderAsync(clientOrderId, symbolPair, OrderSide.Buy, price: limitPrice, size: orderSize, timeoutCts.Token)
            .ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Limit order '{liveOrder}' is live now. Cancel it.").ConfigureAwait(false);
        await tradeClient.CancelOrderAsync(liveOrder, timeoutCts.Token).ConfigureAwait(false);

        // As the cancellation succeeded, this is just a sanity check that should complete almost instantly.
        await Console.Out.WriteLineAsync("Wait for the order to be terminated.").ConfigureAwait(false);
        _ = await liveOrder.WaitUntilClosedAsync(timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }
}