using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Exchanges;

namespace WhalesSecret.ScriptApiLib.Samples.Trading;

/// <summary>
/// Sample allows to place market and limit orders and optionally cancel them in an interactive way.
/// <para>Private connections are necessary to create orders. Exchange API credentials have to be set.</para>
/// </summary>
/// <remarks>IMPORTANT: You have to change the keys and the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class InteractiveTrading : IScriptApiSample
{
    /// <summary>Symbol pair of orders.</summary>
    private static readonly SymbolPair OrderSymbolPair = SymbolPair.BTC_USDT;

    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource connectionTimeoutCts = new(TimeSpan.FromMinutes(1));

        // In order to unlock large orders, a valid license has to be used.
        CreateOptions createOptions = new(license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, connectionTimeoutCts.Token).ConfigureAwait(false);

        // Initialization of the market is required before connection can be created.
        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        ExchangeInfo exchangeInfo = await scriptApi.InitializeMarketAsync(exchangeMarket, connectionTimeoutCts.Token).ConfigureAwait(false);

        // Credentials must be set before we can create a private connection.

#pragma warning disable CA2000 // Dispose objects before losing scope
        IApiIdentity apiIdentity = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => Credentials.GetBinanceHmacApiIdentity(),
            ExchangeMarket.KucoinSpot => Credentials.GetKucoinApiIdentity(),
            _ => throw new SanityCheckException($"Unsupported exchange market {exchangeMarket} provided."),
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        scriptApi.SetCredentials(apiIdentity);

        // Default connection options use full-trading connection type, which means both public and private connections will be established with the exchange.
        ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, ConnectionOptions.Default).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with full-trading access.").ConfigureAwait(false);

        while (true)
        {
            await Console.Out.WriteLineAsync("Please specify an order you want to place.").ConfigureAwait(false);

            OrderType? orderType = await AskForOrderTypeAsync().ConfigureAwait(false);
            if (orderType is null)
                continue;

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            OrderSide? orderSide = await AskForOrderSideAsync().ConfigureAwait(false);
            if (orderSide is null)
                continue;

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            decimal? orderSize = await AskForDecimalValueAsync($"""Specify an order size in {OrderSymbolPair.BaseSymbol}:""").ConfigureAwait(false);
            if (orderSize is null)
                continue;

            string cId = Guid.NewGuid().ToString();

            ILiveOrder liveOrder;

            if (orderType == OrderType.Market)
            {
                using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));
                liveOrder = await tradeClient.CreateMarketOrderAsync(cId, OrderSymbolPair, orderSide.Value, orderSize.Value, timeoutCts.Token).ConfigureAwait(false);
            }
            else if (orderType == OrderType.Limit)
            {
                await Console.Out.WriteLineAsync().ConfigureAwait(false);

                decimal? orderPrice = await AskForDecimalValueAsync($"""Specify a price in {OrderSymbolPair.QuoteSymbol}:""").ConfigureAwait(false);
                if (orderPrice is null)
                    continue;

                using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));
                liveOrder = await tradeClient.CreateLimitOrderAsync(cId, OrderSymbolPair, orderSide.Value, price: orderPrice.Value, size: orderSize.Value, timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            else
            {
                throw new SanityCheckException($"Invalid order type {orderType} provided.");
            }

            bool? cancelOrder = await AskForBoolValueAsync($"Do you want to cancel the order '{liveOrder.ClientOrderId}'? [Y/N]").ConfigureAwait(false);
            if (cancelOrder == true)
            {
                using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));
                await tradeClient.CancelOrderAsync(liveOrder, timeoutCts.Token).ConfigureAwait(false);
            }
            else
            {
                await Console.Out.WriteLineAsync().ConfigureAwait(false);
                await Console.Out.WriteLineAsync($"The order '{liveOrder.ClientOrderId}' will not be canceled.").ConfigureAwait(false);
            }

            await Console.Out.WriteLineAsync().ConfigureAwait(false);

            bool? continuePlacing = await AskForBoolValueAsync("Do you want to place another order? [Y/N]").ConfigureAwait(false);
            if (continuePlacing != true)
                break;
        }

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }

    /// <summary>
    /// Ask the user to provide an order type.
    /// </summary>
    /// <returns>Selected order type, or <c>null</c> if the choice was invalid.</returns>
    private static async Task<OrderType?> AskForOrderTypeAsync()
    {
        string options = """
            Select order type:

            1] Market
            2] Limit
            """;

        await Console.Out.WriteLineAsync(options).ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteAsync("> ").ConfigureAwait(false);
        ConsoleKeyInfo info = Console.ReadKey();
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        OrderType? result = info.KeyChar switch
        {
            '1' => OrderType.Market,
            '2' => OrderType.Limit,
            _ => null,
        };

        if (result is null)
            await Console.Out.WriteLineAsync("Invalid value provided.").ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Ask the user to provide an order side.
    /// </summary>
    /// <returns>Selected order side, or <c>null</c> if the choice was invalid.</returns>
    private static async Task<OrderSide?> AskForOrderSideAsync()
    {
        string options = """
            Select order side:

            1] Buy
            2] Sell
            """;

        await Console.Out.WriteLineAsync(options).ConfigureAwait(false);
        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteAsync("> ").ConfigureAwait(false);
        ConsoleKeyInfo info = Console.ReadKey();
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        OrderSide? result = info.KeyChar switch
        {
            '1' => OrderSide.Buy,
            '2' => OrderSide.Sell,
            _ => null,
        };

        if (result is null)
            await Console.Out.WriteLineAsync("Invalid value provided.").ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Ask the user to provide a decimal value.
    /// </summary>
    /// <returns>Provided decimal value, or <c>null</c> if the value cannot be parsed.</returns>
    private static async Task<decimal?> AskForDecimalValueAsync(string prompt)
    {
        await Console.Out.WriteLineAsync(prompt).ConfigureAwait(false);
        await Console.Out.WriteAsync("> ").ConfigureAwait(false);
        string? line = Console.ReadLine();
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        if (line is null)
            return null;

        if (!decimal.TryParse(line, CultureInfo.InvariantCulture, out decimal value))
        {
            await Console.Out.WriteLineAsync("Failed to parse provided value. Note that decimal separator is to be used to provide input.").ConfigureAwait(false);
            return null;
        }

        return value;
    }

    /// <summary>
    /// Ask the user to provide a bool value.
    /// </summary>
    /// <returns>Provided bool value, or <c>null</c> if the value is not either 'y' or 'n'.</returns>
    private static async Task<bool?> AskForBoolValueAsync(string prompt)
    {
        await Console.Out.WriteLineAsync(prompt).ConfigureAwait(false);
        await Console.Out.WriteAsync("> ").ConfigureAwait(false);
        ConsoleKeyInfo info = Console.ReadKey();
        await Console.Out.WriteLineAsync().ConfigureAwait(false);

        bool? result = info.KeyChar switch
        {
            'Y' => true,
            'y' => true,
            'N' => false,
            'n' => false,
            _ => null,
        };

        if (result is null)
            await Console.Out.WriteLineAsync("Invalid value provided.").ConfigureAwait(false);

        return result;
    }
}