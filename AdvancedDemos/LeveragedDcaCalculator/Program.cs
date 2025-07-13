using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.LeveragedDcaCalculator;

/// <summary>
/// Leveraged DCA (Dollar Cost Averaging) Calculator is an application to calculate profits of Leveraged DCA (L-DCA) strategy.
/// <para>
/// On the input, you provide the exchange market, symbol pair, time-frame, period, order size, order side, trading fee, and leverage, and the application calculates the profit of
/// the L-DCA strategy using the historical data from the exchange.
/// </para>
/// </summary>
internal class Program
{
    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

    /// <summary>
    /// Application that calculates a Leveraged Dollar Cost Averaging (L-DCA) strategy.
    /// </summary>
    /// <param name="args">Command-line arguments.
    /// <para>The program must be started with 1 argument - the input parameters JSON file path.</para>
    /// <para>Example input file:
    /// <code>
    /// {
    ///   "AppDataPath": "Data",
    ///   "ExchangeMarket": "BinanceSpot",
    ///   "SymbolPair": "BTC/EUR",
    ///   "StartTimeUtc": "2024-01-01 00:00:00",
    ///   "EndTimeUtc": "2025-01-01 00:00:00",
    ///   "Period": "1.00:00:00",
    ///   "QuoteSize": 10.0,
    ///   "OrderSide": "Buy",
    ///   "TradeFeePercent": 0.1,
    ///   "Leverage": 2.0,
    ///   "RolloverFeePercent": 0.2,
    ///   "RolloverPeriod": "04:00:00"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// With this input the program will calculate how would L-DCA perform if it was executed on Binance exchange over the whole year 2024, buying <c>10</c> EUR worth of BTC every
    /// day with <c>0.1</c>% trading fee and <c>2</c>x leverage. There is <c>0.2</c>% rollover fee charged every <c>4</c> hours.
    /// </para>
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task Main(string[] args)
    {
        using IDisposable mdlc = clog.SetMdlc();

        clog.Info($"* {nameof(args)}={args.LogJoin()}");

        if (args.Length != 1)
        {
            string markets = string.Join(',', Enum.GetValues<ExchangeMarket>());

            Console.WriteLine($$"""
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(Samples)}}.{{nameof(AdvancedDemos)}}.{{nameof(LeveragedDcaCalculator)}} <parametersFilePath>
                """);

            clog.Info("$<USAGE>");
            clog.FlushAndShutDown();

            return;
        }

        string parametersFilePath = args[0];
        Parameters parameters = Parameters.LoadFromJson(parametersFilePath);

        PrintInfo("Press Ctrl+C to terminate the program.");
        PrintInfo();

        using CancellationTokenSource shutdownCancellationTokenSource = new();
        CancellationToken shutdownToken = shutdownCancellationTokenSource.Token;

        PrintInfo($"Starting calculation of L-DCA on {parameters.ExchangeMarket}, {parameters.OrderSide}ing {parameters.QuoteSize} {parameters.SymbolPair.QuoteSymbol} worth of {
            parameters.SymbolPair.BaseSymbol} every {parameters.Period} with {parameters.Leverage}x leverage and {parameters.TradeFeePercent}% trading fee.");
        PrintInfo();

        // Install Ctrl+C / SIGINT handler.
        ConsoleCancelEventHandler controlCancelHandler = (object? sender, ConsoleCancelEventArgs e) =>
        {
            clog.Info("[CCEH] *");

            // If cancellation of the control event is set to true, the process won't terminate automatically and we will have control over the shutdown.
            e.Cancel = true;
            shutdownCancellationTokenSource.Cancel();

            clog.Info("[CCEH] $");
        };

        Console.CancelKeyPress += controlCancelHandler;

        try
        {
            await StartLDcaCalculationAsync(parameters, shutdownToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            bool handled = false;

            if (e is OperationCanceledException)
            {
                if (shutdownToken.IsCancellationRequested)
                {
                    PrintInfo();
                    PrintInfo("Shutdown detected.");
                    handled = true;
                }
            }

            if (!handled)
            {
                clog.Error($"Exception occurred: {e}");
                throw;
            }
        }
        finally
        {
            // Uninstall Ctrl+C / SIGINT handler.
            Console.CancelKeyPress -= controlCancelHandler;
        }

        clog.Info("$");
        clog.FlushAndShutDown();
    }

    /// <summary>
    /// Prints information level message to the console and to the log. Message timestamp is added when printing to the console unless <paramref name="addTimestamp"/> is set to
    /// <c>false</c>.
    /// </summary>
    /// <param name="msg">Message to print.</param>
    /// <param name="addTimestamp"><c>true</c> to add message timestamp when printing to the console, <c>false</c> otherwise.</param>
    private static void PrintInfo(string msg = "", bool addTimestamp = true)
    {
        clog.Info(msg);

        if (msg.Length > 0)
        {
            if (addTimestamp)
            {
                DateTime dateTime = DateTime.UtcNow;
                string dateTimeStr = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                Console.WriteLine($"{dateTimeStr}: {msg}");
            }
            else Console.WriteLine(msg);
        }
        else Console.WriteLine();
    }

    /// <summary>
    /// Starts the L-DCA calculation.
    /// </summary>
    /// <param name="parameters">Program parameters.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="AlreadyExistsException">Thrown if another instance is already operating on the given data folder.</exception>
    /// <exception cref="ConnectionInitializationException">Thrown if the connection could not be fully established and initialized with the remote exchange server.</exception>
    /// <exception cref="ExchangeIsUnderMaintenanceException">Thrown if the exchange is in the maintenance mode and does not accept new connections.</exception>
    /// <exception cref="FileAccessException">Thrown if it is not possible to create the data folder.</exception>
    /// <exception cref="InvalidProductLicenseException">Thrown if the license provided in the options is invalid.</exception>
    /// <exception cref="MalfunctionException">Thrown if the initialization of core components fails or another unexpected error occurred.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled including cancellation due to shutdown or object disposal.</exception>
    /// <exception cref="OperationFailedException">Thrown if the request could not be sent to the exchange.</exception>
    private static async Task StartLDcaCalculationAsync(Parameters parameters, CancellationToken cancellationToken)
    {
        clog.Debug($"* {nameof(parameters)}='{parameters}'");

        CreateOptions createOptions = new(appDataFolder: parameters.AppDataPath);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, cancellationToken).ConfigureAwait(false);

        PrintInfo($"Connect to {parameters.ExchangeMarket} exchange with market-data access.");

        ConnectionOptions connectionOptions = new(BlockUntilReconnectedOrTimeout.InfinityTimeoutInstance, ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(parameters.ExchangeMarket, connectionOptions).ConfigureAwait(false);

        PrintInfo($"Connection to {parameters.ExchangeMarket} has been established successfully.");

        DateTime startTime = parameters.StartTimeUtc;
        DateTime endTime = parameters.EndTimeUtc;
        SymbolPair symbolPair = parameters.SymbolPair;
        List<Candle> candles = await DownloadCandlesAsync(tradeClient, symbolPair, startTime: startTime, endTime: endTime, cancellationToken).ConfigureAwait(false);

        decimal tradeFee = parameters.TradeFeePercent / 100m;
        decimal rolloverFee = parameters.RolloverFeePercent / 100m;

        // Use the request builder for rounding calculations.
        OrderRequestBuilder<MarketOrderRequest> orderRequestBuilder = tradeClient.CreateOrderRequestBuilder<MarketOrderRequest>();

        _ = LDcaInternal(candles, orderRequestBuilder, tradeFee, parameters.SymbolPair, parameters.OrderSide, quoteSize: parameters.QuoteSize, parameters.Period,
            leverage: parameters.Leverage, rolloverFee: rolloverFee, rolloverPeriod: parameters.RolloverPeriod);

        clog.Debug("$");
    }

    /// <summary>
    /// Calculates the Leveraged Dollar Cost Averaging (L-DCA) strategy using the provided candles and parameters.
    /// </summary>
    /// <param name="candles">1-minute candles covering the requested time-frame.</param>
    /// <param name="orderRequestBuilder">Request builder that is used for rounding of order sizes and prices.</param>
    /// <param name="tradeFee">Trade fee.</param>
    /// <param name="symbolPair">Symbol pair being traded.</param>
    /// <param name="orderSide">Side of the orders.</param>
    /// <param name="quoteSize">Size of the orders in the quote symbol.</param>
    /// <param name="period">Time period in between the orders.</param>
    /// <param name="leverage">Leverage of the trades.</param>
    /// <param name="rolloverFee">Rollover fee, or <c>0</c> if no rollover fee should be calculated or if <paramref name="leverage"/> is equal to <c>1.0</c>.</param>
    /// <param name="rolloverPeriod">Frequency with which the rollover fee is charged.</param>
    /// <returns>Result of the calculation.</returns>
    internal static LdcaResult LDcaInternal(List<Candle> candles, OrderRequestBuilder<MarketOrderRequest> orderRequestBuilder, decimal tradeFee, SymbolPair symbolPair,
        OrderSide orderSide, decimal quoteSize, TimeSpan period, decimal leverage, decimal rolloverFee, TimeSpan rolloverPeriod)
    {
        decimal tradeFeesPaid = 0m;
        decimal rolloverFeesPaid = 0m;

        decimal baseSymbolBalance = 0m;
        decimal quoteSymbolBalance = 0m;

        _ = orderRequestBuilder
            .SetSymbolPair(symbolPair)
            .SetSide(orderSide);

        bool useLeverage = leverage > 1.0m;
        string feeSymbol = useLeverage
            ? symbolPair.QuoteSymbol
            : orderSide == OrderSide.Buy ? symbolPair.BaseSymbol : symbolPair.QuoteSymbol;

        DateTime nextOrderTime = DateTime.MinValue;
        decimal totalInitialMargin = 0m;

        decimal totalBaseAmount = 0m;
        decimal totalQuoteAmount = 0m;

        List<LeveragedOrderInfo> leveragedOrders = new(capacity: candles.Count);

        DateTime prevCandleTime = DateTime.MinValue;
        foreach (Candle candle in candles)
        {
            DateTime time = candle.Timestamp;

            if (useLeverage)
            {
                decimal lowPrice = candle.LowPrice;
                decimal highPrice = candle.HighPrice;

                List<LeveragedOrderInfo> ordersToRemove = new();
                foreach (LeveragedOrderInfo existingLeveragedOrder in leveragedOrders)
                {
                    // If there is a rollover fee.
                    if (rolloverFee != 0)
                    {
                        // If the rollover fee has not been paid yet, the first payment is due one rollover period after the open time of the order. Otherwise, it is due one rollover
                        // period after the last payment.
                        DateTime nextRolloverPaymentTime = existingLeveragedOrder.LastRolloverFeePaidTimeUtc is null
                            ? existingLeveragedOrder.OpenTimeUtc.Add(rolloverPeriod)
                            : existingLeveragedOrder.LastRolloverFeePaidTimeUtc.Value.Add(rolloverPeriod);

                        // If we are processing a candle that just reached the next rollover fee payment time, we pay the fee.
                        if ((prevCandleTime < nextRolloverPaymentTime) && (nextRolloverPaymentTime <= time))
                        {
                            existingLeveragedOrder.LastRolloverFeePaidTimeUtc = nextRolloverPaymentTime;
                            decimal rolloverFeeToPay = (existingLeveragedOrder.PositionQuoteAmount - existingLeveragedOrder.InitialMargin) * rolloverFee;
                            rolloverFeesPaid += rolloverFeeToPay;
                            quoteSymbolBalance -= rolloverFeeToPay;
                        }
                    }

                    bool isLiquidated = ((orderSide == OrderSide.Buy) && (existingLeveragedOrder.LiquidationPrice >= lowPrice))
                        || ((orderSide == OrderSide.Sell) && (existingLeveragedOrder.LiquidationPrice <= highPrice));

                    if (isLiquidated)
                    {
                        if (orderSide == OrderSide.Buy)
                        {
                            PrintInfo($"  x {candle.Timestamp:yyyy-MM-dd HH:mm:ss}: Leveraged order '{existingLeveragedOrder}' has been liquidated. Low price reached {lowPrice} {
                                symbolPair.QuoteSymbol}.", addTimestamp: false);
                        }
                        else
                        {
                            PrintInfo($"  x {candle.Timestamp:yyyy-MM-dd HH:mm:ss}: Leveraged order '{existingLeveragedOrder}' has been liquidated. High price reached {highPrice} {
                                symbolPair.QuoteSymbol}.", addTimestamp: false);
                        }

                        ordersToRemove.Add(existingLeveragedOrder);
                        continue;
                    }
                }

                foreach (LeveragedOrderInfo orderToRemove in ordersToRemove)
                    _ = leveragedOrders.Remove(orderToRemove);
            }

            if (nextOrderTime <= time)
            {
                // Simulate an order with the price in the middle of the candle.
                decimal price = (candle.OpenPrice + candle.ClosePrice) / 2;

                if (!useLeverage)
                {
                    // Order without leverage. Standard DCA on spot market.
                    decimal intendedOrderQuoteSize = quoteSize;
                    decimal intendedOrderBaseSize = intendedOrderQuoteSize / price;

                    MarketOrderRequest marketOrderRequest = orderRequestBuilder
                        .SetSizeInBaseSymbol(sizeInBaseSymbol: true)
                        .SetSize(intendedOrderBaseSize)
                        .Build();

                    decimal actualOrderBaseSize = marketOrderRequest.Size;
                    decimal actualOrderQuoteSize = actualOrderBaseSize * price;

                    totalBaseAmount += actualOrderBaseSize;
                    totalQuoteAmount += actualOrderQuoteSize;

                    decimal feeAmount = orderSide == OrderSide.Buy ? tradeFee * actualOrderBaseSize : tradeFee * actualOrderQuoteSize;
                    tradeFeesPaid += feeAmount;

                    baseSymbolBalance += orderSide == OrderSide.Buy ? actualOrderBaseSize - feeAmount : -actualOrderBaseSize;
                    quoteSymbolBalance += orderSide == OrderSide.Buy ? -actualOrderQuoteSize : actualOrderQuoteSize - feeAmount;

                    PrintInfo($"  * {candle.Timestamp:yyyy-MM-dd HH:mm:ss}: {(orderSide == OrderSide.Buy ? "Bought" : "Sold")} {actualOrderBaseSize} {symbolPair.BaseSymbol} @ {
                        price} {symbolPair.QuoteSymbol} for total of {actualOrderQuoteSize} {symbolPair.QuoteSymbol}, fee was {feeAmount} {feeSymbol}. Current balance is {
                        baseSymbolBalance} {symbolPair.BaseSymbol} and {quoteSymbolBalance} {symbolPair.QuoteSymbol}.", addTimestamp: false);

                    nextOrderTime = time.Add(period);
                }
                else
                {
                    // Leveraged order.
                    decimal intendedOrderQuoteSize = quoteSize * leverage;
                    decimal intendedOrderBaseSize = intendedOrderQuoteSize / price;

                    MarketOrderRequest marketOrderRequest = orderRequestBuilder
                        .SetSizeInBaseSymbol(sizeInBaseSymbol: true)
                        .SetSize(intendedOrderBaseSize)
                        .Build();

                    decimal actualOrderBaseSize = marketOrderRequest.Size;
                    decimal actualOrderQuoteSize = actualOrderBaseSize * price;

                    totalBaseAmount += actualOrderBaseSize;
                    totalQuoteAmount += actualOrderQuoteSize;

                    decimal initialMargin = actualOrderQuoteSize / leverage;
                    totalInitialMargin += initialMargin;

                    decimal liquidationPrice = orderSide == OrderSide.Buy ? price * (1m - (1m / leverage)) : price * (1m + (1m / leverage));

                    decimal feeAmount = tradeFee * actualOrderQuoteSize;
                    tradeFeesPaid += feeAmount;

                    quoteSymbolBalance += -initialMargin - feeAmount;

                    PrintInfo($"  * {candle.Timestamp:yyyy-MM-dd HH:mm:ss}: {(orderSide == OrderSide.Buy ? "Bought" : "Sold")} {actualOrderBaseSize} {symbolPair.BaseSymbol} @ {
                        price} {symbolPair.QuoteSymbol}. The position size is {actualOrderQuoteSize} {symbolPair.QuoteSymbol}, initial margin is {initialMargin} {
                        symbolPair.QuoteSymbol}, fee was {feeAmount} {feeSymbol} and liquidation price is {liquidationPrice} {symbolPair.QuoteSymbol}. Current balance is {
                        quoteSymbolBalance} {symbolPair.QuoteSymbol} and total initial margin is {totalInitialMargin}.",
                        addTimestamp: false);

                    LeveragedOrderInfo leveragedOrder = new(entryPrice: price, initialMargin: initialMargin, positionBaseAmount: actualOrderBaseSize,
                        positionQuoteAmount: actualOrderQuoteSize, liquidationPrice: liquidationPrice, openTimeUtc: candle.Timestamp);

                    leveragedOrders.Add(leveragedOrder);

                    nextOrderTime = time.Add(period);
                }
            }

            prevCandleTime = time;
        }

        decimal finalPrice = candles[^1].ClosePrice;
        PrintInfo();
        PrintInfo($"Final price: {finalPrice} {symbolPair.QuoteSymbol}");

        decimal averageOrderPrice, profitPercent, totalInvestedAmount, totalValue;

        if (!useLeverage)
        {
            PrintInfo();
            PrintInfo($"Final balance: {baseSymbolBalance} {symbolPair.BaseSymbol}, {quoteSymbolBalance} {symbolPair.QuoteSymbol}.");
            PrintInfo($"Total fees paid: {tradeFeesPaid} {feeSymbol}.");

            averageOrderPrice = totalQuoteAmount / totalBaseAmount;
            PrintInfo($"Average order price: {averageOrderPrice} {symbolPair.QuoteSymbol}.");

            if (orderSide == OrderSide.Buy)
            {
                decimal baseSymbolValue = baseSymbolBalance * finalPrice;
                totalValue = baseSymbolValue + quoteSymbolBalance;
                PrintInfo($"Total value: {baseSymbolValue} {symbolPair.QuoteSymbol} + {quoteSymbolBalance} {symbolPair.QuoteSymbol} = {totalValue} {symbolPair.QuoteSymbol}");

                profitPercent = 100m * totalValue / -quoteSymbolBalance;
                PrintInfo($"Profit: {profitPercent:0.000}%");

                totalInvestedAmount = -quoteSymbolBalance;
            }
            else
            {
                decimal quoteSymbolValue = quoteSymbolBalance / finalPrice;
                totalValue = baseSymbolBalance + quoteSymbolValue;
                PrintInfo($"Total value: {quoteSymbolValue} {symbolPair.BaseSymbol} + {baseSymbolBalance} {symbolPair.BaseSymbol} = {totalValue} {symbolPair.BaseSymbol}");

                profitPercent = 100m * totalValue / -baseSymbolBalance;
                PrintInfo($"Profit: {profitPercent:0.000}%");

                totalInvestedAmount = -baseSymbolBalance;
            }
        }
        else
        {
            // To calculate the profit of leveraged orders, we simulate closing the orders.
            totalValue = 0m;
            foreach (LeveragedOrderInfo leveragedOrder in leveragedOrders)
            {
                decimal positionValue = leveragedOrder.PositionBaseAmount * finalPrice;
                totalValue += positionValue;

                decimal positionProfit = orderSide == OrderSide.Buy ? positionValue - leveragedOrder.PositionQuoteAmount : leveragedOrder.PositionQuoteAmount - positionValue;
                quoteSymbolBalance += leveragedOrder.InitialMargin + positionProfit;
            }

            PrintInfo();
            PrintInfo($"Final balance: {quoteSymbolBalance} {symbolPair.QuoteSymbol}.");
            PrintInfo($"Total trading fees paid: {tradeFeesPaid} {feeSymbol}.");
            PrintInfo($"Total rollover fees paid: {rolloverFeesPaid} {feeSymbol}.");
            PrintInfo($"Total funds needed: {totalInitialMargin} {symbolPair.QuoteSymbol}");

            averageOrderPrice = totalQuoteAmount / totalBaseAmount;
            PrintInfo($"Average order price: {averageOrderPrice} {symbolPair.QuoteSymbol}.");

            profitPercent = 100m * ((quoteSymbolBalance - totalInitialMargin) / totalInitialMargin);
            PrintInfo($"Profit: {profitPercent:0.000}%");

            totalInvestedAmount = totalInitialMargin;
        }

        LdcaResult result = new(finalPrice: finalPrice, finalBaseBalance: baseSymbolBalance, finalQuoteBalance: quoteSymbolBalance, tradeFeesPaid: tradeFeesPaid,
            feeSymbol: feeSymbol, averageOrderPrice: averageOrderPrice, totalValue: totalValue, totalInvestedAmount: totalInvestedAmount, profitPercent: profitPercent,
            rolloverFeesPaid: rolloverFeesPaid);

        clog.Debug($"$='{result}'");
        return result;
    }

    /// <summary>
    /// Downloads 1-minute candles for the given symbol pair from the exchange between the specified start and end times.
    /// </summary>
    /// <param name="tradeClient">Connected trade API client.</param>
    /// <param name="symbolPair">Symbol pair to download.</param>
    /// <param name="startTime">UTC timestamp (inclusive) of the start of the interval for which to download candles.</param>
    /// <param name="endTime">UTC timestamp (exclusive) of the end of the interval for which to download candles.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>List of 1-minute candles that cover the requested period.</returns>
    /// <remarks>The method downloads the candles in chunks of 14 days to be able to report progress regularly.</remarks>
    private static async Task<List<Candle>> DownloadCandlesAsync(ITradeApiClient tradeClient, SymbolPair symbolPair, DateTime startTime, DateTime endTime,
        CancellationToken cancellationToken)
    {
        clog.Debug($"* {nameof(startTime)}={startTime},{nameof(endTime)}={endTime},");

        TimeSpan timeSpan = endTime - startTime;
        List<Candle> result = new(capacity: (int)timeSpan.TotalMinutes + 1);

        DateTime queryStartTime = startTime;
        DateTime queryEndTime = queryStartTime.AddDays(14);

        if (queryEndTime > endTime)
            queryEndTime = endTime;

        while (queryStartTime < endTime)
        {
            PrintInfo($"Downloading historical data for '{symbolPair}' between {queryStartTime:yyyy-MM-dd HH:mm:ss} and {queryEndTime:yyyy-MM-dd HH:mm:ss}.");

            CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(symbolPair, CandleWidth.Minute1, startTime: queryStartTime, endTime: queryEndTime,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            foreach (Candle candle in candlestickData.Candles)
                result.Add(candle);

            queryStartTime = queryEndTime;
            queryEndTime += TimeSpan.FromDays(14);

            if (queryEndTime > endTime)
                queryEndTime = endTime;
        }

        clog.Debug($"|$|={result.Count}");
        return result;
    }
}