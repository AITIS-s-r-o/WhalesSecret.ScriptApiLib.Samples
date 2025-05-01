using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.AccessLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

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
        Console.WriteLine($"Connect to {exchangeMarket} exchange with full-trading access.");

        while (true)
        {
            Console.WriteLine("Please specify an order you want to place.");

            OrderType? orderType = AskForOrderType();
            if (orderType is null)
                continue;

            Console.WriteLine();

            OrderSide? orderSide = AskForOrderSide();
            if (orderSide is null)
                continue;

            Console.WriteLine();

            decimal? orderSize = AskForDecimalValue($"""Specify an order size in {OrderSymbolPair.BaseSymbol}:""");
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
                Console.WriteLine();

                decimal? orderPrice = AskForDecimalValue($"""Specify a price in {OrderSymbolPair.QuoteSymbol}:""");
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

            bool? cancelOrder = AskForBoolValue($"Do you want to cancel the order '{liveOrder.ClientOrderId}'? [Y/N]");
            if (cancelOrder == true)
            {
                using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(1));
                await tradeClient.CancelOrderAsync(liveOrder, timeoutCts.Token).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"The order '{liveOrder.ClientOrderId}' will not be canceled.");
            }

            Console.WriteLine();

            bool? continuePlacing = AskForBoolValue("Do you want to place another order? [Y/N]");
            if (continuePlacing != true)
                break;
        }

        Console.WriteLine();
        Console.WriteLine("Disposing trade API client and script API.");
    }

    /// <summary>
    /// Ask the user to provide an order type.
    /// </summary>
    /// <returns>Selected order type, or <c>null</c> if the choice was invalid.</returns>
    private static OrderType? AskForOrderType()
    {
        string options = """
            Select order type:

            1] Market
            2] Limit
            """;

        Console.WriteLine(options);
        Console.WriteLine();
        Console.Write("> ");
        ConsoleKeyInfo info = Console.ReadKey();
        Console.WriteLine();

        OrderType? result = info.KeyChar switch
        {
            '1' => OrderType.Market,
            '2' => OrderType.Limit,
            _ => null,
        };

        if (result is null)
            Console.WriteLine("Invalid value provided.");

        return result;
    }

    /// <summary>
    /// Ask the user to provide an order side.
    /// </summary>
    /// <returns>Selected order side, or <c>null</c> if the choice was invalid.</returns>
    private static OrderSide? AskForOrderSide()
    {
        string options = """
            Select order side:

            1] Buy
            2] Sell
            """;

        Console.WriteLine(options);
        Console.WriteLine();
        Console.Write("> ");
        ConsoleKeyInfo info = Console.ReadKey();
        Console.WriteLine();

        OrderSide? result = info.KeyChar switch
        {
            '1' => OrderSide.Buy,
            '2' => OrderSide.Sell,
            _ => null,
        };

        if (result is null)
            Console.WriteLine("Invalid value provided.");

        return result;
    }

    /// <summary>
    /// Ask the user to provide a decimal value.
    /// </summary>
    /// <returns>Provided decimal value, or <c>null</c> if the value cannot be parsed.</returns>
    private static decimal? AskForDecimalValue(string prompt)
    {
        Console.WriteLine(prompt);
        Console.Write("> ");
        string? line = Console.ReadLine();
        Console.WriteLine();

        if (line is null)
            return null;

        if (!decimal.TryParse(line, CultureInfo.InvariantCulture, out decimal value))
        {
            Console.WriteLine("Failed to parse provided value. Note that decimal separator is to be used to provide input.");
            return null;
        }

        return value;
    }

    /// <summary>
    /// Ask the user to provide a bool value.
    /// </summary>
    /// <returns>Provided bool value, or <c>null</c> if the value is not either 'y' or 'n'.</returns>
    private static bool? AskForBoolValue(string prompt)
    {
        Console.WriteLine(prompt);
        Console.Write("> ");
        ConsoleKeyInfo info = Console.ReadKey();
        Console.WriteLine();

        bool? result = info.KeyChar switch
        {
            'Y' => true,
            'y' => true,
            'N' => false,
            'n' => false,
            _ => null,
        };

        if (result is null)
            Console.WriteLine("Invalid value provided.");

        return result;
    }
}