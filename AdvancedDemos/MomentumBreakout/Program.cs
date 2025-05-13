using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.VisualStudio.Threading;
using Skender.Stock.Indicators;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.SharedLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders.Brackets.Updates;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.MomentumBreakout;

/// <summary>
/// Momentum Breakout trading bot. This bot monitors short-period and long-period Exponential Moving Averages (EMAs) and looks for momentum breakouts. Each trade must be confirmed
/// by Relative Strength Index (RSI) indicator to avoid buying in overbought conditions and selling in oversold conditions. Further, we require certain minimal volume to confirm
/// strength of the breakout. The bot also uses Average True Range (ATR) to measure volatility and set dynamic stop-loss and take-profit levels.
/// <para>
/// The trade entry rules for the long positions:
/// <list type="bullet">
/// <item>Trend Condition: The <see cref="Parameters.ShortEmaLookback"/>-period EMA is above the <see cref="Parameters.LongEmaLookback"/>-period EMA (bullish trend).</item>
/// <item>Breakout Condition: Price breaks above the high of the previous <see cref="Parameters.BreakoutLookback"/> candles by at least <see cref="Parameters.BreakoutAtrSize"/>
/// times ATR.</item>
/// <item>Momentum Confirmation: RSI (with period <see cref="Parameters.RsiLookback"/>) is above 50 but below 70 (to avoid overbought conditions).</item>
/// <item>Volume Confirmation: Current candle volume is at least <see cref="Parameters.VolumeAvgSize"/> times the average volume of the last <see cref="Parameters.VolumeLookback"/>
/// candles.</item>
/// <item>Volatility Confirmation: Avoid trading during low volatility, that is ATR less than <see cref="Parameters.VolatilityAvgSize"/> times the average ATR over
/// <see cref="Parameters.VolatilityLookback"/> candles.</item>
/// </list>
/// </para>
/// <para>
/// The trade entry rules for the short positions:
/// <list type="bullet">
/// <item>Trend Condition: The <see cref="Parameters.ShortEmaLookback"/>-period EMA is below the <see cref="Parameters.LongEmaLookback"/>-period EMA (bearish trend).</item>
/// <item>Breakout Condition: Price breaks below the low of the previous <see cref="Parameters.BreakoutLookback"/> candles by at least <see cref="Parameters.BreakoutAtrSize"/>
/// times ATR.</item>
/// <item>Momentum Confirmation: RSI (with period <see cref="Parameters.RsiLookback"/>) is below 50 but above 30 (to avoid oversold conditions).</item>
/// <item>Volume Confirmation: Current candle volume is at least <see cref="Parameters.VolumeAvgSize"/> times the average volume of the last <see cref="Parameters.VolumeLookback"/>
/// candles.</item>
/// <item>Volatility Confirmation: Avoid trading during low volatility, that is ATR less than <see cref="Parameters.VolatilityAvgSize"/> times the average ATR over
/// <see cref="Parameters.VolatilityLookback"/> candles.</item>
/// </list>
/// </para>
/// <para>The bot makes a maximum of <see cref="Parameters.MaxTradesPerDay"/> trades per day.</para>
/// <para>
/// Each position uses <see cref="Parameters.StopLossCount"/> stop-loss orders and <see cref="Parameters.TakeProfitCount"/> take-profit orders. The first stop-loss order is
/// placed at the distance of <see cref="Parameters.FirstStopLossAtr"/> times ATR from the entry price. The first take-profit order is placed at the distance of
/// <see cref="Parameters.FirstTakeProfitAtr"/> times ATR from the entry price. The next stop-loss orders are placed at <see cref="Parameters.NextStopLossAtrIncrement"/> times ATR
/// from the previous stop-loss. The next take-profit order are placed at <see cref="Parameters.NextTakeProfitAtrIncrement"/> times ATR from the previous take-profit.
/// </para>
/// <para>
/// Each trade uses the size that is <see cref="Parameters.PositionSize"/> times the original available budget for the base symbol of <see cref="Parameters.SymbolPair"/>.
/// </para>
/// <para>
/// If all entry conditions are satisfied and the bot attempts to open a new position, the bot enters a cooldown period for <see cref="Parameters.TradeCooldownPeriod"/> candles
/// in order to avoid duplicated trades. The bot may operate multiple open positions at the same time, but all open position must be on the same side - either all long or all
/// short.
/// </para>
/// <para>The bot also create reports about its performance and writes the report history it into a CSV file.</para>
/// </summary>
internal class Program
{
    /// <summary>Name of the trading strategy.</summary>
    private const string StrategyName = "MomentumBreakout";

    /// <summary>Name of the report file.</summary>
    private const string ReportFileName = $"{StrategyName}-budgetReport.csv";

    /// <summary>Live bracketed orders termination tasks mapped to the bracketed orders.</summary>
    /// <remarks>All access has to be protected by <see cref="liveLock"/>.</remarks>
    private static readonly Dictionary<ILiveBracketedOrder, Task> liveBracketedOrdersTerminationTasksMap = new();

    /// <summary>Event that is raised when a new bracketed order is placed.</summary>
    private static readonly AsyncAutoResetEvent newLiveBracketedOrder = new();

    /// <summary>List of UTC times when the positions have been opened. This is used to limit the number of trades per day.</summary>
    /// <seealso cref="Parameters.MaxTradesPerDay"/>
    private static readonly List<DateTime> positionTimes = new();

    /// <summary>
    /// Lock object to be used when accessing <see cref="liveBracketedOrdersTerminationTasksMap"/>, <see cref="openPositions"/>, <see cref="workingOrderAvgFillPrice"/>,
    /// <see cref="stopLossFilledWeight"/>, and <see cref="takeProfitFilledWeight"/>.
    /// </summary>
    private static readonly Lock liveLock = new();

    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

    /// <summary>Cancellation token that is canceled when the shutdown is initiated, or <c>null</c> if not initialized yet.</summary>
    private static CancellationToken? shutdownToken;

    /// <summary>Telegram API instance, or <c>null</c> if not initialized yet.</summary>
    private static Telegram? telegram;

    /// <summary>Number of trades the bot performed.</summary>
    private static int tradeCounter;

    /// <summary>If <see cref="openPositions"/> is greater than <c>0</c>, this is the order side of all the open positions.</summary>
    private static OrderSide positionSide;

    /// <summary>Number of currently opened positions.</summary>
    /// <remarks>All access has to be protected by <see cref="liveLock"/>.</remarks>
    private static int openPositions;

    /// <summary>Average price of the working order's fills, or <c>0</c> if the information is not available.</summary>
    /// <remarks>All access has to be protected by <see cref="liveLock"/>.</remarks>
    private static decimal workingOrderAvgFillPrice;

    /// <summary>Cumulative weight of all stop-loss fills.</summary>
    /// <remarks>
    /// Weight of a fill is calculated as its price distance from the <see cref="workingOrderAvgFillPrice">working order average fill price</see> multiplied by the price of
    /// the fill.
    /// <para>All access has to be protected by <see cref="liveLock"/>.</para>
    /// </remarks>
    private static decimal stopLossFilledWeight;

    /// <summary>Cumulative weight of all take-profit fills.</summary>
    /// <remarks>All access has to be protected by <see cref="liveLock"/>.</remarks>
    /// <seealso cref="stopLossFilledWeight"/>
    private static decimal takeProfitFilledWeight;

    /// <summary>
    /// Application that trades a Direct Cost Averaging (DCA) strategy.
    /// </summary>
    /// <param name="args">Command-line arguments.
    /// <para>The program must be started with 1 argument - the input parameters JSON file path.</para>
    /// <para>Example input file:
    /// <code>
    /// {
    ///   "AppDataPath": "Data",
    ///   "ExchangeMarket": "BinanceSpot",
    ///   "SymbolPair": "BTC/EUR",
    ///   "ShortEmaLookback": 20,
    ///   "LongEmaLookback": 50,
    ///   "RsiLookback": 14,
    ///   "AtrLookback": 14,
    ///   "BreakoutLookback": 3,
    ///   "BreakoutAtrSize": 0.5,
    ///   "VolumeLookback": 10,
    ///   "VolumeAvgSize": 1.5,
    ///   "VolatilityLookback": 20,
    ///   "VolatilityAvgSize": 0.5,
    ///   "MaxTradesPerDay": 10,
    ///   "MaxOpenPositions": 1,
    ///   "CandleWidth": "Minutes5",
    ///   "StopLossCount": 2,
    ///   "TakeProfitCount": 2,
    ///   "FirstStopLossAtr": 1.5,
    ///   "NextStopLossAtrIncrement": 0.5,
    ///   "FirstTakeProfitAtr": 2.0,
    ///   "NextTakeProfitAtrIncrement": 1.0,
    ///   "PositionSize": 0.02,
    ///   "TradeCooldownPeriod": 3,
    ///   "OrderIdPrefix": "MB5m",
    ///   "BudgetRequest": {
    ///     "StrategyName": "MomentumBreakout",
    ///     "PrimaryAsset": "EUR",
    ///     "InitialBudget": {
    ///       "EUR": 1000,
    ///       "BTC": 0.01
    ///     }
    ///   },
    ///   "ReportPeriod": "12:00:00"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// With this input the program will open positions that are <c>2%</c> of the initial budget - i.e. <c>20</c> EUR worth of BTC for buy orders and <c>0.0002</c> BTC for sell
    /// orders. Each position will have two stop-loss brackets and two take-profit brackets, each having <c>50%</c> size of the position. The report will be generated twice a day.
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
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(Samples)}}.{{nameof(AdvancedDemos)}}.{{nameof(MomentumBreakout)}} <parametersFilePath>
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
        shutdownToken = shutdownCancellationTokenSource.Token;

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
            await RunStrategyAsync(parameters, shutdownToken.Value).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            bool handled = false;

            if (e is OperationCanceledException)
            {
                if (shutdownToken.Value.IsCancellationRequested)
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
    /// Prints information level message to the console and to the log. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="message">Message to print.</param>
    private static void PrintInfo(string message = "")
    {
        clog.Info(message);

        if (message.Length > 0)
        {
            string dateTimeStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Console.WriteLine($"{dateTimeStr}: {message}");
        }
        else Console.WriteLine();
    }

    /// <summary>
    /// Prints error level message to the console and to the log. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="msg">Message to print.</param>
    private static void PrintError(string msg = "")
    {
        clog.Error(msg);

        if (msg.Length > 0)
        {
            string dateTimeStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Console.WriteLine($"{dateTimeStr}: ERROR {msg}");
        }
        else Console.WriteLine();
    }

    /// <summary>
    /// Prints information message to the console and to the log and sends the message to Telegram. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="message">Message to print.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PrintInfoTelegramAsync(string message, CancellationToken cancellationToken)
    {
        PrintInfo(message);

        if ((telegram is not null) && !string.IsNullOrEmpty(message))
        {
            string? error = await telegram.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            if (error is not null)
                clog.Error($"Sending message to Telegram failed. {error}");
        }
    }

    /// <summary>
    /// Prints error message to the console and to the log and sends the message to Telegram. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="message">Message to print.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PrintErrorTelegramAsync(string message, CancellationToken cancellationToken)
    {
        PrintError(message);

        if ((telegram is not null) && !string.IsNullOrEmpty(message))
        {
            string? error = await telegram.SendMessageAsync($"<b>ERROR:</b> {message}", cancellationToken).ConfigureAwait(false);
            if (error is not null)
                clog.Error($"Sending message to Telegram failed. {error}");
        }
    }

    /// <summary>
    /// Starts the DCA trading bot.
    /// </summary>
    /// <param name="parameters">Program parameters.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="AlreadyExistsException">Thrown if another instance is already operating on the given data folder.</exception>
    /// <exception cref="BudgetCalculationException">Thrown if a trading strategy budget is associated with the trade API client and it is not possible to calculate whether
    /// an order would exceed it if it was placed.</exception>
    /// <exception cref="BudgetExceededException">Thrown if a trading strategy budget is associated with the trade API client and placing the order would exceed the budget.
    /// </exception>
    /// <exception cref="ConnectionInitializationException">Thrown if the connection could not be fully established and initialized with the remote exchange server.</exception>
    /// <exception cref="ExchangeIsUnderMaintenanceException">Thrown if the exchange is in the maintenance mode and does not accept new connections.</exception>
    /// <exception cref="FileAccessException">Thrown if it is not possible to create the data folder.</exception>
    /// <exception cref="InvalidProductLicenseException">Thrown if the license provided in the options is invalid.</exception>
    /// <exception cref="MalfunctionException">Thrown if the initialization of core components fails or another unexpected error occurred.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled including cancellation due to shutdown or object disposal.</exception>
    /// <exception cref="OperationFailedException">Thrown if the request could not be sent to the exchange.</exception>
    private static async Task RunStrategyAsync(Parameters parameters, CancellationToken cancellationToken)
    {
        clog.Debug($"* {nameof(parameters)}='{parameters}'");

        // In order to unlock large orders, a valid license has to be used.
        CreateOptions createOptions = new(appDataFolder: parameters.AppDataPath, license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2000 // Dispose objects before losing scope
        IApiIdentity apiIdentity = parameters.ExchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => Credentials.GetBinanceHmacApiIdentity(),
            ExchangeMarket.KucoinSpot => Credentials.GetKucoinApiIdentity(),
            _ => throw new SanityCheckException($"Unsupported exchange market {parameters.ExchangeMarket} provided."),
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        scriptApi.SetCredentials(apiIdentity);

        telegram = new(groupId: Credentials.TelegramGroupId, apiToken: Credentials.TelegramApiToken);
        await using Telegram telegramToDispose = telegram;

        await PrintIntroAsync(parameters, cancellationToken).ConfigureAwait(false);
        await PrintInfoTelegramAsync($"Connect to {parameters.ExchangeMarket} exchange with full-trading access.", cancellationToken).ConfigureAwait(false);

        ConnectionOptions connectionOptions = new(BlockUntilReconnectedOrTimeout.InfinityTimeoutInstance, ConnectionType.FullTrading, OnConnectedAsync, OnDisconnectedAsync,
            budgetRequest: parameters.BudgetRequest);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(parameters.ExchangeMarket, connectionOptions).ConfigureAwait(false);

        await PrintInfoTelegramAsync($"Connection to {parameters.ExchangeMarket} has been established successfully.", cancellationToken).ConfigureAwait(false);

        string reportFilePath = Path.Combine(parameters.AppDataPath, ReportFileName);
        Task reportTask = RunReportTaskAsync(reportFilePath, tradeClient, parameters.ReportPeriod, cancellationToken);
        Task bracketedOrderTerminationMonitoringTask = RunBracketedOrderTerminationMonitoringTaskAsync(cancellationToken);

        int candlesNeeded = CalculateNumberOfCandles(parameters);

        CandleWidth candleWidth = parameters.CandleWidth;
        if (!CandleWidthToTimeSpan(candleWidth, out TimeSpan? candleTimeSpan))
            throw new SanityCheckException($"Unable to convert candle width {candleWidth} to timespan.");

        TimeSpan historyNeeded = candleTimeSpan.Value * (candlesNeeded + 1);
        DateTime now = DateTime.UtcNow;
        DateTime startTime = now.Add(-historyNeeded);
        CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(parameters.SymbolPair, candleWidth, startTime: startTime, endTime: now, cancellationToken)
            .ConfigureAwait(false);

        int counter = 0;
        int quotesBuffer = 100;
        int maxQuotes = candlestickData.Candles.Count + quotesBuffer;
        List<Quote> quotes = InitializeQuotesAndIndicatorValues(parameters, candlestickData.Candles, maxQuotes, out double? currentShortEma, out double? currentLongEma,
            out double? currentRsi, out double? currentAtr);

        decimal? currentVolume = null;

        await using ITickerSubscription tickerSubscription = await tradeClient.CreateTickerSubscriptionAsync(parameters.SymbolPair).ConfigureAwait(false);
        await using ICandlestickSubscription candleSubscription = await tradeClient.CreateCandlestickSubscriptionAsync(parameters.SymbolPair).ConfigureAwait(false);

        Task<Candle> candleTask = candleSubscription.WaitNextClosedCandlestickAsync(candleWidth, cancellationToken);
        Task<CandleUpdate> candleUpdateTask = candleSubscription.WaitNextCandlestickUpdateAsync(candleWidth, cancellationToken);
        Task<Ticker> tickerTask = tickerSubscription.GetNewerTickerAsync(cancellationToken);

        // List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.
        List<string> tradeConditionLogs = new(capacity: 32);

        OrderRequestBuilder<MarketOrderRequest> orderRequestBuilder = tradeClient.CreateOrderRequestBuilder<MarketOrderRequest>();
        _ = orderRequestBuilder
            .SetSizeInBaseSymbol(true)
            .SetSymbolPair(parameters.SymbolPair);

        DateTime nextEntry = DateTime.MinValue;
        try
        {
            while (true)
            {
                _ = await Task.WhenAny(candleTask, candleUpdateTask, tickerTask).ConfigureAwait(false);

                if (candleTask.IsCompleted)
                {
                    Candle closedCandle = await candleTask.ConfigureAwait(false);
                    clog.Debug($"New closed candle received: {closedCandle}");

                    ProcessClosedCandle(closedCandle, quotes, maxQuotes: maxQuotes, quotesBuffer: quotesBuffer, parameters, ref currentShortEma, ref currentLongEma, ref currentRsi,
                        ref currentAtr);

                    // Refresh task.
                    candleTask = candleSubscription.WaitNextClosedCandlestickAsync(candleWidth, cancellationToken);
                }

                if (candleUpdateTask.IsCompleted)
                {
                    CandleUpdate candleUpdate = await candleUpdateTask.ConfigureAwait(false);
                    currentVolume = candleUpdate.Candle.BaseVolume;

                    // Refresh task.
                    candleUpdateTask = candleSubscription.WaitNextCandlestickUpdateAsync(candleWidth, cancellationToken);
                }

                if (tickerTask.IsCompleted)
                {
                    Ticker ticker = await tickerTask.ConfigureAwait(false);
                    if (ticker.LastPrice is not null)
                    {
                        if (DateTime.UtcNow > nextEntry)
                        {
                            counter++;
                            tradeConditionLogs.Clear();

                            // Once in a while we print more logs, just to be able to check progress in logs.
                            bool debugIteration = (counter % 20) == 0;

                            decimal lastPrice = ticker.LastPrice.Value;
                            tradeConditionLogs.Add($"  Current price: {lastPrice}");
                            if (debugIteration)
                                clog.Trace($"Current price is {lastPrice}.");

                            if ((currentShortEma is not null) && (currentLongEma is not null) && (currentRsi is not null) && (currentAtr is not null)
                                && (currentVolume is not null))
                            {
                                bool entry = await ProcessNewPriceAsync(tradeClient, orderRequestBuilder, quotes, lastPrice: lastPrice,
                                    currentShortEma: (decimal)currentShortEma.Value, currentLongEma: (decimal)currentLongEma.Value, currentRsi: (decimal)currentRsi.Value,
                                    currentAtr: (decimal)currentAtr.Value, currentVolume: currentVolume.Value, parameters, debugIteration, tradeConditionLogs, cancellationToken)
                                    .ConfigureAwait(false);

                                if (entry)
                                {
                                    DateTime lastEntry = DateTime.UtcNow;
                                    nextEntry = lastEntry.Add(parameters.TradeCooldownPeriod * candleTimeSpan.Value);
                                    await PrintInfoTelegramAsync($"New trade has been attempted. Cooldown period of {
                                        parameters.TradeCooldownPeriod} candles activated. Next trade entry time set to {nextEntry}.", cancellationToken).ConfigureAwait(false);
                                }
                            }
                            else clog.Trace("Waiting for the required values for calculation to be available.");
                        }
                        else clog.Debug($"Cooldown in progress. Waiting until {nextEntry} before considering making a new trade.");
                    }
                    else clog.Warn("Receive ticker does not have the last price.");

                    // Refresh task.
                    tickerTask = tickerSubscription.GetNewerTickerAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            await PrintInfoTelegramAsync("Shutdown detected.", CancellationToken.None).ConfigureAwait(false);
        }

        try
        {
            clog.Debug("Wait until all tasks are finished.");
            await Task.WhenAll(reportTask, bracketedOrderTerminationMonitoringTask, candleTask, candleUpdateTask, tickerTask).ConfigureAwait(false);
        }
        catch
        {
        }

        clog.Debug("$");
    }

    /// <summary>
    /// Initializes list of quotes to be used for calculating indicator values, and initializes main indicator values.
    /// </summary>
    /// <param name="parameters">Program parameters.</param>
    /// <param name="candles">List of historical candles to be converted to quotes.</param>
    /// <param name="maxQuotes">Maximum number of quotes.</param>
    /// <param name="currentShortEma">Variable to be filled with the current short EMA value.</param>
    /// <param name="currentLongEma">Variable to be filled with the current long EMA value.</param>
    /// <param name="currentRsi">Variable to be filled with the current RSI value.</param>
    /// <param name="currentAtr">Variable to be filled with the current ATR value.</param>
    /// <returns>List of quotes that correspond to the input candles.</returns>
    private static List<Quote> InitializeQuotesAndIndicatorValues(Parameters parameters, IReadOnlyList<Candle> candles, int maxQuotes, out double? currentShortEma,
        out double? currentLongEma, out double? currentRsi, out double? currentAtr)
    {
        List<Quote> quotes = new(capacity: maxQuotes);

        foreach (Candle candle in candles)
            quotes.Add(QuoteFromCandle(candle));

        IEnumerable<EmaResult> shortEmaResult = quotes.GetEma(parameters.ShortEmaLookback);
        IEnumerable<EmaResult> longEmaResult = quotes.GetEma(parameters.LongEmaLookback);
        IEnumerable<RsiResult> rsiResult = quotes.GetRsi(parameters.RsiLookback);
        IEnumerable<AtrResult> atrResult = quotes.GetAtr(parameters.AtrLookback);

        currentShortEma = shortEmaResult.Last()?.Ema;
        currentLongEma = longEmaResult.Last()?.Ema;
        currentRsi = rsiResult.Last()?.Rsi;
        currentAtr = atrResult.Last()?.Atr;

        return quotes;
    }

    /// <summary>
    /// Calculates how many candles are needed to perform the required calculations given the specific program parameters.
    /// </summary>
    /// <param name="parameters">Program parameters.</param>
    /// <returns>Number of candles needed to perform the required calculations.</returns>
    private static int CalculateNumberOfCandles(Parameters parameters)
    {
        int candlesNeeded = parameters.LongEmaLookback;

        if (parameters.RsiLookback > candlesNeeded)
            candlesNeeded = parameters.RsiLookback;

        if (parameters.VolumeLookback > candlesNeeded)
            candlesNeeded = parameters.VolumeLookback;

        // When we calculate volatility we need to have at least VolatilityLookback valid values, and for each value we need at least VolatilityLookback previous candles,
        // therefore we need two times VolatilityLookback candles.
        if (parameters.VolatilityLookback > candlesNeeded)
            candlesNeeded = 2 * parameters.VolatilityLookback;

        return candlesNeeded;
    }

    /// <summary>
    /// Processes a newly received closed candle.
    /// </summary>
    /// <param name="closedCandle">Closed candle to process.</param>
    /// <param name="quotes">List of quotes to add the new candle values to.</param>
    /// <param name="maxQuotes">Maximum number of quotes.</param>
    /// <param name="quotesBuffer">Number of extra quotes in <paramref name="quotes"/> that are not needed for calculation.</param>
    /// <param name="parameters">Program parameters.</param>
    /// <param name="currentShortEma">Variable to be filled with the current short EMA value.</param>
    /// <param name="currentLongEma">Variable to be filled with the current long EMA value.</param>
    /// <param name="currentRsi">Variable to be filled with the current RSI value.</param>
    /// <param name="currentAtr">Variable to be filled with the current ATR value.</param>
    private static void ProcessClosedCandle(Candle closedCandle, List<Quote> quotes, int maxQuotes, int quotesBuffer, Parameters parameters, ref double? currentShortEma,
        ref double? currentLongEma, ref double? currentRsi, ref double? currentAtr)
    {
        Quote quote = QuoteFromCandle(closedCandle);
        if (quote.Date == quotes[^1].Date)
        {
            // Do not add candle that we already have.
            return;
        }

        quotes.Add(QuoteFromCandle(closedCandle));
        if (quotes.Count > maxQuotes)
            quotes.RemoveRange(index: 0, count: quotesBuffer);

        IEnumerable<EmaResult> shortEmaResult = quotes.GetEma(parameters.ShortEmaLookback);
        IEnumerable<EmaResult> longEmaResult = quotes.GetEma(parameters.LongEmaLookback);
        IEnumerable<RsiResult> rsiResult = quotes.GetRsi(parameters.RsiLookback);
        IEnumerable<AtrResult> atrResult = quotes.GetAtr(parameters.AtrLookback);

        currentShortEma = shortEmaResult.Last()?.Ema;
        currentLongEma = longEmaResult.Last()?.Ema;
        currentRsi = rsiResult.Last()?.Rsi;
        currentAtr = atrResult.Last()?.Atr;

        if ((currentShortEma is not null) && (currentLongEma is not null) && (currentRsi is not null) && (currentAtr is not null))
        {
            clog.Debug($"Current short({parameters.ShortEmaLookback})-EMA, long({parameters.LongEmaLookback})-EMA, RSI, ATR is {currentShortEma}, {currentLongEma}, {currentRsi}, {
                currentAtr}.");
        }
        else
        {
            if (currentShortEma is null)
                clog.Warn("Unable to calculate the current short EMA.");

            if (currentLongEma is null)
                clog.Warn("Unable to calculate the current long EMA.");

            if (currentRsi is null)
                clog.Warn("Unable to calculate the current RSI.");

            if (currentAtr is null)
                clog.Warn("Unable to calculate the current ATR.");
        }
    }

    /// <summary>
    /// Checks the breakout condition. For long entry, it means that the price breaks above the high of the previous <paramref name="breakoutLookback"/> candles by at least
    /// <paramref name="breakoutAtrSize"/> times ATR. For short entry, it means that the price breaks below the low of the previous <paramref name="breakoutLookback"/> candles
    /// by at least <paramref name="breakoutAtrSize"/> times ATR.
    /// </summary>
    /// <param name="longEntry"><c>true</c> to check for the long entry breakout condition, <c>false</c> to check for the short entry.</param>
    /// <param name="quotes">List of quotes to use to check the breakout condition.</param>
    /// <param name="lastPrice">Latest price.</param>
    /// <param name="breakoutLookback">Number of candles for breakout confirmation.</param>
    /// <param name="breakoutAtrSize">Size of the breakout in multiples of ATR required for confirmation of the breakout.</param>
    /// <param name="currentAtr">Current value of ATR.</param>
    /// <param name="verbose"><c>true</c> to produce extra trace logs, <c>false</c> otherwise.</param>
    /// <param name="tradeConditionLogs">List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.</param>
    /// <returns><c>true</c> of the breakout is confirmed, <c>false</c> otherwise.</returns>
    private static bool CheckBreakoutCondition(bool longEntry, List<Quote> quotes, decimal lastPrice, int breakoutLookback, decimal breakoutAtrSize, decimal currentAtr,
        bool verbose, List<string> tradeConditionLogs)
    {
        if (verbose)
        {
            clog.Trace($"* {nameof(longEntry)}={longEntry},|{nameof(quotes)}|={quotes.Count},{nameof(lastPrice)}={lastPrice},{nameof(breakoutLookback)}={breakoutLookback},{
                nameof(breakoutAtrSize)}={breakoutAtrSize},{nameof(currentAtr)}={currentAtr},{nameof(verbose)}={verbose}");
        }

        IEnumerable<decimal> breakoutSeries;

        bool result;
        if (longEntry)
        {
            // The highest price over last breakout-lookback period.
            breakoutSeries = quotes.TakeLast(breakoutLookback).Select(c => c.High);
            decimal breakoutHigh = breakoutSeries.Max();
            decimal breakoutPrice = breakoutHigh + (breakoutAtrSize * currentAtr);

            result = lastPrice > breakoutPrice;
            if (result)
            {
                clog.Debug($"Last price {lastPrice} is above the breakout price {breakoutPrice} on bullish trend.");

                tradeConditionLogs.Add("  Bullish breakout");
                tradeConditionLogs.Add($"    {breakoutLookback}-breakout series: {breakoutSeries.LogJoin()}");
                tradeConditionLogs.Add($"    Breakout high: {breakoutHigh}");
                tradeConditionLogs.Add($"    Breakout price: {breakoutPrice}");
            }
            else if (verbose)
            {
                clog.Trace($"On bullish trend, current price {lastPrice} did not breakout price {breakoutPrice} with {breakoutLookback}-high being at {breakoutHigh} and ATR at {
                    currentAtr}.");
            }
        }
        else
        {
            // The lowest price over last breakout-lookback period.
            breakoutSeries = quotes.TakeLast(breakoutLookback).Select(c => c.Low);
            decimal breakoutLow = breakoutSeries.Min();
            decimal breakoutPrice = breakoutLow - (breakoutAtrSize * currentAtr);

            result = lastPrice < breakoutPrice;

            if (result)
            {
                clog.Debug($"Last price {lastPrice} is below the breakout price {breakoutPrice} on bearish trend.");

                tradeConditionLogs.Add("  Bearish breakout");
                tradeConditionLogs.Add($"    {breakoutLookback}-breakout series: {breakoutSeries.LogJoin()}");
                tradeConditionLogs.Add($"    Breakout low: {breakoutLow}");
                tradeConditionLogs.Add($"    Breakout price: {breakoutPrice}");
            }
            else if (verbose)
            {
                clog.Trace($"On bearish trend, the current price {lastPrice} did not breakout price {breakoutPrice} with {breakoutLookback}-low being at {breakoutLow} and ATR at {
                    currentAtr}.");
            }
        }

        if (verbose)
            clog.Trace($"$={result}");
        return result;
    }

    /// <summary>
    /// Checks the RSI condition. For long entry, the current RSI is above <c>50</c> but below <c>70</c> (to avoid overbought conditions). For short entry, the current RSI
    /// is below <c>50</c> but above <c>30</c> (to avoid oversold conditions).
    /// </summary>
    /// <param name="longEntry"><c>true</c> to check for the long entry breakout condition, <c>false</c> to check for the short entry.</param>
    /// <param name="quotes">List of quotes to use to check the breakout condition.</param>
    /// <param name="currentRsi">Latest RSI value.</param>
    /// <param name="verbose"><c>true</c> to produce extra trace logs, <c>false</c> otherwise.</param>
    /// <param name="tradeConditionLogs">List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.</param>
    /// <returns><c>true</c> of the RSI condition is confirmed, <c>false</c> otherwise.</returns>
    private static bool CheckRsiCondition(bool longEntry, List<Quote> quotes, decimal currentRsi, bool verbose, List<string> tradeConditionLogs)
    {
        if (verbose)
            clog.Trace($"* {nameof(longEntry)}={longEntry},|{nameof(quotes)}|={quotes.Count},{nameof(currentRsi)}={currentRsi}");

        bool result;
        if (longEntry)
        {
            // RSI confirms bullish trend, but is not overbought.
            result = (50 < currentRsi) && (currentRsi < 70);

            if (result)
            {
                clog.Debug($"RSI at {currentRsi} confirms the bullish trend without being overbought.");
            }
            else if (verbose)
            {
                clog.Debug($"RSI at {currentRsi} does NOT confirm the bullish trend without being overbought.");
            }
        }
        else
        {
            // RSI confirms bearish trend, but is not oversold.
            result = (30 < currentRsi) && (currentRsi < 50);

            if (result)
            {
                clog.Debug($"RSI at {currentRsi} confirms the bearish trend without being oversold.");
            }
            else if (verbose)
            {
                clog.Debug($"RSI at {currentRsi} does NOT confirm the bearish trend without being oversold.");
            }
        }

        if (result)
        {
            tradeConditionLogs.Add("  RSI");
            tradeConditionLogs.Add($"    Current RSI: {currentRsi}");
        }

        if (verbose)
            clog.Trace($"$={result}");
        return result;
    }

    /// <summary>
    /// Checks the volume condition. The current candle volume must be at least <paramref name="volumeAvgSize"/> times the average volume of the last
    /// <paramref name="volumeLookback"/> candles.
    /// </summary>
    /// <param name="quotes">List of quotes to use to check the breakout condition.</param>
    /// <param name="volumeLookback">Number of candles for volume confirmation.</param>
    /// <param name="volumeAvgSize">Size of the current volume in multiples of volume average over <paramref name="volumeLookback"/> period required for volume confirmation.
    /// </param>
    /// <param name="currentVolume">Current candle volume.</param>
    /// <param name="verbose"><c>true</c> to produce extra trace logs, <c>false</c> otherwise.</param>
    /// <param name="tradeConditionLogs">List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.</param>
    /// <returns><c>true</c> of the volume condition is confirmed, <c>false</c> otherwise.</returns>
    private static bool CheckVolumeCondition(List<Quote> quotes, int volumeLookback, decimal volumeAvgSize, decimal currentVolume, bool verbose, List<string> tradeConditionLogs)
    {
        if (verbose)
        {
            clog.Trace($"* |{nameof(quotes)}|={quotes.Count},{nameof(volumeLookback)}={volumeLookback},{nameof(volumeAvgSize)}={volumeAvgSize},{
                nameof(currentVolume)}={currentVolume}");
        }

        // Average volume on the last candles.
        IEnumerable<decimal> volumeSeries = quotes.TakeLast(volumeLookback).Select(c => c.Volume);
        decimal averageVolume = volumeSeries.Average();
        decimal volumeBreakout = volumeAvgSize * averageVolume;
        bool result = currentVolume > volumeBreakout;

        if (result)
        {
            clog.Debug($"Current volume {currentVolume} is greater than the required volume {volumeBreakout} (avg. volume {averageVolume} times multiplier {volumeAvgSize}).");

            tradeConditionLogs.Add("  Volume");
            tradeConditionLogs.Add($"    {volumeLookback}-volume series: {volumeSeries.LogJoin()}");
            tradeConditionLogs.Add($"    Average volume: {averageVolume}");
            tradeConditionLogs.Add($"    Volume factor: {volumeAvgSize}");
            tradeConditionLogs.Add($"    Current volume: {currentVolume}");
            tradeConditionLogs.Add($"    Required volume: {volumeBreakout}");
        }
        else if (verbose)
        {
            clog.Trace($"Current volume {currentVolume} is NOT greater than the required volume {volumeBreakout} (avg. volume {averageVolume} times multiplier {volumeAvgSize}).");
        }

        if (verbose)
            clog.Trace($"$={result}");
        return result;
    }

    /// <summary>
    /// Checks the volatility condition. ATR has to be at least than <paramref name="volatilityAvgSize"/> times the average ATR over <paramref name="volatilityLookback"/> candles.
    /// </summary>
    /// <param name="quotes">List of quotes to use to check the breakout condition.</param>
    /// <param name="volatilityLookback">Number of candles for volatility confirmation.</param>
    /// <param name="volatilityAvgSize">Size of the current ATR in multiples of the ATR average over <paramref name="volatilityLookback"/> period required for volatility
    /// confirmation.</param>
    /// <param name="currentAtr">Current ATR.</param>
    /// <param name="verbose"><c>true</c> to produce extra trace logs, <c>false</c> otherwise.</param>
    /// <param name="tradeConditionLogs">List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.</param>
    /// <returns><c>true</c> of the volatility condition is confirmed, <c>false</c> otherwise.</returns>
    private static bool CheckVolatilityCondition(List<Quote> quotes, int volatilityLookback, decimal volatilityAvgSize, decimal currentAtr, bool verbose,
        List<string> tradeConditionLogs)
    {
        if (verbose)
        {
            clog.Trace($"* |{nameof(quotes)}|={quotes.Count},{nameof(volatilityLookback)}={volatilityLookback},{nameof(volatilityAvgSize)}={volatilityAvgSize},{
                nameof(currentAtr)}={currentAtr},{nameof(verbose)}={verbose}");
        }

        bool result = false;

        // Average volume on the last candles.
        IEnumerable<AtrResult> atrResults = quotes.GetAtr(lookbackPeriods: volatilityLookback).TakeLast(volatilityLookback);
        if (atrResults.Any(c => c.Atr is null))
        {
            if (verbose)
                clog.Trace($"$<NOT_AVAILABLE>={result}");

            return result;
        }

        IEnumerable<decimal> atrSeries = atrResults.Select(c => (decimal)c.Atr!.Value);
        decimal averageAtr = atrSeries.Average();
        decimal requiredAtr = averageAtr * volatilityAvgSize;

        result = currentAtr > requiredAtr;

        if (result)
        {
            clog.Debug($"Current ATR {currentAtr} is greater than the required ATR {requiredAtr} (avg. ATR {averageAtr} times multiplier {volatilityAvgSize}).");

            tradeConditionLogs.Add("  Volatility");
            tradeConditionLogs.Add($"    {volatilityLookback}-ATR series: {atrSeries.LogJoin()}");
            tradeConditionLogs.Add($"    Average ATR: {averageAtr}");
            tradeConditionLogs.Add($"    Volatility factor: {volatilityAvgSize}");
            tradeConditionLogs.Add($"    Current ATR: {currentAtr}");
            tradeConditionLogs.Add($"    Required ATR: {requiredAtr}");
        }
        else if (verbose)
        {
            clog.Trace($"Current ATR {currentAtr} is NOT greater than the required ATR {requiredAtr} (avg. ATR {averageAtr} times multiplier {volatilityAvgSize}).");
        }

        if (verbose)
            clog.Trace($"$={result}");
        return result;
    }

    /// <summary>
    /// Processes new market price.
    /// </summary>
    /// <param name="tradeClient">Connected API client.</param>
    /// <param name="orderRequestBuilder">Order request builder for working orders to open the trading position with.</param>
    /// <param name="quotes">List of quotes needed for calculations.</param>
    /// <param name="lastPrice">Price of the last trade.</param>
    /// <param name="currentShortEma">Current short EMA value.</param>
    /// <param name="currentLongEma">Current long EMA value.</param>
    /// <param name="currentRsi">Current RSI value.</param>
    /// <param name="currentAtr">Current ATR value.</param>
    /// <param name="currentVolume">Current volume.</param>
    /// <param name="parameters">Program parameters.</param>
    /// <param name="verbose"><c>true</c> to produce extra trace logs, <c>false</c> otherwise.</param>
    /// <param name="tradeConditionLogs">List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns><c>true</c> if the cooldown should be activated , <c>false</c> otherwise.</returns>
    private static async Task<bool> ProcessNewPriceAsync(ITradeApiClient tradeClient, OrderRequestBuilder<MarketOrderRequest> orderRequestBuilder, List<Quote> quotes,
        decimal lastPrice, decimal currentShortEma, decimal currentLongEma, decimal currentRsi, decimal currentAtr, decimal currentVolume, Parameters parameters, bool verbose,
        List<string> tradeConditionLogs, CancellationToken cancellationToken)
    {
        if (verbose)
        {
            clog.Trace($"* {nameof(tradeClient)}='{tradeClient}',|{quotes}|={quotes.Count},{nameof(lastPrice)}={lastPrice},{nameof(currentShortEma)}={currentShortEma},{
                nameof(currentLongEma)}={currentLongEma},{nameof(currentRsi)}={currentRsi},{nameof(currentAtr)}={currentAtr},{nameof(currentVolume)}={currentVolume},{
                nameof(parameters)}='{parameters}',{nameof(verbose)}={verbose},|{nameof(tradeConditionLogs)}|={tradeConditionLogs.Count}");
        }

        // Short EMA over the long EMA implies bullish trend. Short EMA under the long EMA implies bearish trend.
        bool bullishTrend = currentShortEma > currentLongEma;
        bool bearishTrend = currentShortEma < currentLongEma;

        bool result = false;
        if (bullishTrend || bearishTrend)
        {
            tradeConditionLogs.Add($"  Trend: {(bullishTrend ? "bullish" : "bearish")}");
            tradeConditionLogs.Add($"    {parameters.ShortEmaLookback}-EMA: {currentShortEma}");
            tradeConditionLogs.Add($"    {parameters.LongEmaLookback}-EMA: {currentLongEma}");

            bool breakoutConfirmation = CheckBreakoutCondition(longEntry: bullishTrend, quotes, lastPrice: lastPrice, breakoutLookback: parameters.BreakoutLookback,
                breakoutAtrSize: parameters.BreakoutAtrSize, currentAtr: currentAtr, verbose: verbose, tradeConditionLogs);

            bool rsiConfirmation = CheckRsiCondition(longEntry: bullishTrend, quotes, currentRsi, verbose: verbose, tradeConditionLogs);

            bool volumeConfirmation = CheckVolumeCondition(quotes, volumeLookback: parameters.VolumeLookback, volumeAvgSize: parameters.VolatilityAvgSize,
                currentVolume: currentVolume, verbose: verbose, tradeConditionLogs);

            bool volatilityConfirmation = CheckVolatilityCondition(quotes, volatilityLookback: parameters.VolatilityLookback, volatilityAvgSize: parameters.VolatilityAvgSize,
                currentAtr: currentAtr, verbose: verbose, tradeConditionLogs);

            if (breakoutConfirmation && rsiConfirmation && volatilityConfirmation && volatilityConfirmation)
            {
                int positions;
                lock (liveLock)
                {
                    positions = openPositions;
                }

                OrderSide orderSide = bullishTrend ? OrderSide.Buy : OrderSide.Sell;
                if (((positions == 0) || (positionSide == orderSide))
                    && (positions < parameters.MaxOpenPositions))
                {
                    DateTime dayAgo = DateTime.UtcNow.AddDays(-1);

                    clog.Debug($"Number of timestamps in position-times list is {positionTimes.Count}/{parameters.MaxTradesPerDay}, the latest timestamp is {positionTimes}.");
                    if ((positionTimes.Count < parameters.MaxTradesPerDay) || (positionTimes[0] < dayAgo))
                    {
                        await PlaceOrderAsync(tradeClient, parameters, orderSide, lastPrice: lastPrice, currentAtr: currentAtr, orderRequestBuilder, tradeConditionLogs,
                            cancellationToken).ConfigureAwait(false);

                        // Activate cooldown even if the order was not open.
                        result = true;
                    }
                    else
                    {
                        await PrintInfoTelegramAsync($"Cannot open new position because we have already created {parameters.MaxTradesPerDay} positions in the last 24 hours.",
                            cancellationToken).ConfigureAwait(false);

                        // Activate cooldown even if the order was not open.
                        result = true;
                    }
                }
                else
                {
                    await PrintInfoTelegramAsync($"Cannot open {(orderSide == OrderSide.Buy ? "long" : "short")} position because we have {positions} open {
                        (positionSide == OrderSide.Buy ? "long" : "short")} positions. Maximum number of open positions is {parameters.MaxOpenPositions}.", cancellationToken)
                        .ConfigureAwait(false);

                    // Activate cooldown even if the order was not open.
                    result = true;
                }
            }
            else if (verbose)
            {
                clog.Trace($$"""
                    Entry conditions are not satisfied:
                        Breakout:   {{(breakoutConfirmation ? "PASSED" : "FAILED")}}
                        RSI:        {{(rsiConfirmation ? "PASSED" : "FAILED")}}
                        Volume:     {{(volumeConfirmation ? "PASSED" : "FAILED")}}
                        Volatility: {{(volatilityConfirmation ? "PASSED" : "FAILED")}}
                    """);
            }
        }
        else clog.Trace("Current short-EMA equals long-EMA. No trend detected.");

        if (verbose)
            clog.Trace($"$={result}");
        return result;
    }

    /// <summary>
    /// Creates a new bracketed order.
    /// </summary>
    /// <param name="tradeClient">Connected API client.</param>
    /// <param name="parameters">Program parameters.</param>
    /// <param name="orderSide">Side of the order to create.</param>
    /// <param name="lastPrice">Price of the last trade.</param>
    /// <param name="currentAtr">Current ATR value.</param>
    /// <param name="orderRequestBuilder">Order request builder for working orders to open the trading position with.</param>
    /// <param name="tradeConditionLogs">List of human readable log messages to be sent to Telegram in case the trade entry conditions are met.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PlaceOrderAsync(ITradeApiClient tradeClient, Parameters parameters, OrderSide orderSide, decimal lastPrice, decimal currentAtr,
        OrderRequestBuilder<MarketOrderRequest> orderRequestBuilder, List<string> tradeConditionLogs, CancellationToken cancellationToken)
    {
        tradeCounter++;
        clog.Debug($"All entry conditions are satisfied, attempt entering the trade #{tradeCounter}.");

        string clientOrderId = $"{parameters.OrderIdPrefix}{tradeCounter:00000}{(orderSide == OrderSide.Buy ? 'b' : 's')}{ITradingStrategyBudget.ClientOrderIdSuffix}";

        string symbol = orderSide == OrderSide.Buy ? parameters.SymbolPair.QuoteSymbol : parameters.SymbolPair.BaseSymbol;
        if (!parameters.BudgetRequest.InitialBudget.TryGetValue(symbol, out decimal initialBudget))
            throw new SanityCheckException($"Initial budget has no allocation for '{symbol}'");

        decimal orderSize = initialBudget * parameters.PositionSize;
        decimal orderSizeInBaseSymbol = orderSide == OrderSide.Buy ? orderSize / lastPrice : orderSize;
        MarketOrderRequest workingOrderRequest = orderRequestBuilder
            .SetSide(orderSide)
            .SetSize(orderSizeInBaseSymbol)
            .SetClientOrderId(clientOrderId)
            .Build();

        tradeConditionLogs.Add("  Working order:");
        tradeConditionLogs.Add($"    {workingOrderRequest}");

        BracketOrderDefinition[] bracketOrdersDefinitions = CreateBracketOrdersDefinitions(orderSide, lastPrice: lastPrice, currentAtr: currentAtr, parameters);

        tradeConditionLogs.Add("  Bracket orders:");
        foreach (BracketOrderDefinition bracketOrderDefinition in bracketOrdersDefinitions)
            tradeConditionLogs.Add($"    {bracketOrderDefinition}");

        try
        {
            ILiveBracketedOrder liveBracketedOrder = await tradeClient.CreateBracketedOrderAsync(workingOrderRequest, bracketOrdersDefinitions, OnBracketedOrderUpdateAsync,
                cancellationToken).ConfigureAwait(false);

            Task orderTerminationTask = liveBracketedOrder.TerminatedEvent.WaitAsync(cancellationToken);
            lock (liveLock)
            {
                liveBracketedOrdersTerminationTasksMap.Add(liveBracketedOrder, orderTerminationTask);
                clog.Debug($"Termination task for bracketed order '{liveBracketedOrder}' has been added to the map.");
            }

            positionSide = orderSide;

            int positions;
            lock (liveLock)
            {
                openPositions++;
                positions = openPositions;
            }

            positionTimes.Add(DateTime.UtcNow);
            if (positionTimes.Count > parameters.MaxTradesPerDay)
                positionTimes.RemoveAt(index: 0);

            newLiveBracketedOrder.Set();

            await PrintTradeSummaryAsync(tradeConditionLogs, liveBracketedOrder, positions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            string encodedException = HttpUtility.HtmlEncode(e.ToString());
            await PrintErrorTelegramAsync($"Creating a new bracketed order with working order request '{workingOrderRequest}' failed with exception: {encodedException}",
                cancellationToken).ConfigureAwait(false);

            // Activate cooldown even if the order was not open.
        }
    }

    /// <summary>
    /// Prints summary of the trade to the console and Telegram.
    /// </summary>
    /// <param name="tradeConditionLogs">Trade logs to print.</param>
    /// <param name="liveBracketedOrder">Live bracketed order that was created.</param>
    /// <param name="positions">Number of currently open positions.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PrintTradeSummaryAsync(List<string> tradeConditionLogs, ILiveBracketedOrder liveBracketedOrder, int positions, CancellationToken cancellationToken)
    {
        StringBuilder stringBuilder = new();
        _ = stringBuilder
            .AppendLine("All entry conditions are satisfied.")
            .AppendLine("<pre>");

        foreach (string line in tradeConditionLogs)
            _ = stringBuilder.AppendLine(line);

        _ = stringBuilder
            .AppendLine("</pre>")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Bracketed order '{liveBracketedOrder}' has been placed.")
            .AppendLine(CultureInfo.InvariantCulture, $"We have {positions} open {(positionSide == OrderSide.Buy ? "long" : "short")} positions.");

        string msg = stringBuilder.ToString();

        await PrintInfoTelegramAsync(msg, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IBracketedOrdersFactory.OnBracketedOrderUpdateAsync"/>
    /// <remarks>
    /// We use fire and forget in this method for sending a message to Telegram as we do not want to block the live bracketed order flow in case there is a problem on Telegram's
    /// side.
    /// </remarks>
    private static Task OnBracketedOrderUpdateAsync(IBracketedOrderUpdate update)
    {
        clog.Debug($"* {nameof(update)}='{update}'");

        if (shutdownToken is null)
            throw new SanityCheckException("Shutdown token is not initialized.");

        switch (update)
        {
            case WorkingOrderCreated workingOrderCreated:
                _ = PrintInfoTelegramAsync($"Working order '{workingOrderCreated.ClientOrderId}' of live bracketed order '{workingOrderCreated.Order}' has been created.",
                    CancellationToken.None);
                break;

            case WorkingOrderFill workingOrderFill:
            {
                if (workingOrderFill.Fills.Count > 0)
                {
                    StringBuilder stringBuilder = new();
                    string msg = $"Working order '{workingOrderFill.ClientOrderId}' of live bracketed order '{workingOrderFill.Order}' has been filled:";
                    _ = stringBuilder
                        .AppendLine(msg)
                        .AppendLine("<code>");

                    foreach (FillData fillData in workingOrderFill.Fills)
                        _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  {fillData}");

                    _ = stringBuilder.AppendLine("</code>");
                    _ = stringBuilder.AppendLine();

                    lock (liveLock)
                    {
                        decimal? cumAvgPrice = workingOrderFill.Fills[^1].CumulativeAveragePrice;
                        workingOrderAvgFillPrice = cumAvgPrice ?? 0;
                        _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Working order fill average price is {cumAvgPrice}.");
                    }

                    _ = PrintInfoTelegramAsync(stringBuilder.ToString(), CancellationToken.None);
                }
                else
                {
                    _ = PrintInfoTelegramAsync($"Working order '{workingOrderFill.ClientOrderId}' of live bracketed order '{
                        workingOrderFill.Order}' has been filled completely.", CancellationToken.None);
                }

                break;
            }

            case WorkingOrderCanceled workingOrderCanceled:
                _ = PrintInfoTelegramAsync($"Working order '{workingOrderCanceled.ClientOrderId}' has been canceled.", CancellationToken.None);
                break;

            case BracketOrderCreated bracketOrderCreated:
                _ = PrintInfoTelegramAsync($"{(bracketOrderCreated.BracketOrderType == BracketOrderType.StopLoss ? "Stop-loss" : "Take-profit")} #{
                    bracketOrderCreated.Index} bracket order '{bracketOrderCreated.ClientOrderId}' created for live bracketed order '{bracketOrderCreated.Order}'.",
                    CancellationToken.None);
                break;

            case BracketOrderFill bracketOrderFill:
            {
                string type = bracketOrderFill.BracketOrderType == BracketOrderType.StopLoss ? "Stop-loss" : "Take-profit";
                if (bracketOrderFill.Fills.Count > 0)
                {
                    StringBuilder stringBuilder = new();
                    string msg = $"{type} #{bracketOrderFill.Index} bracket order '{bracketOrderFill.ClientOrderId}' of live bracketed order '{
                        bracketOrderFill.Order}' has been filled:";

                    _ = stringBuilder
                        .AppendLine(msg)
                        .AppendLine("<code>");

                    decimal slWeight, tpWeight;
                    lock (liveLock)
                    {
                        clog.Trace($"Current stop-loss filled weight is {stopLossFilledWeight}, take-profit filled weight is {
                            takeProfitFilledWeight}, working order average filled price is {workingOrderAvgFillPrice}.");

                        foreach (FillData fillData in bracketOrderFill.Fills)
                        {
                            _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  {fillData}");

                            if ((workingOrderAvgFillPrice != 0) && (fillData.LastAveragePrice is not null))
                            {
                                decimal priceDiff = Math.Abs(fillData.LastAveragePrice.Value - workingOrderAvgFillPrice);
                                decimal weight = priceDiff * fillData.LastSize;

                                clog.Trace($"Last price is {fillData.LastAveragePrice.Value}, price difference is {priceDiff}, last size is {fillData.LastSize}, filled weight is {
                                    weight}.");

                                if (bracketOrderFill.BracketOrderType == BracketOrderType.StopLoss) stopLossFilledWeight += weight;
                                else takeProfitFilledWeight += weight;
                            }
                        }

                        slWeight = stopLossFilledWeight;
                        tpWeight = takeProfitFilledWeight;
                    }

                    _ = stringBuilder.AppendLine("</code>");
                    _ = stringBuilder.AppendLine();

                    decimal pnlWeight = tpWeight - slWeight;
                    _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture,
                        $"New total stop-loss weight is {slWeight}, take-profit weight is {tpWeight}, PnL weight is {pnlWeight}.");

                    _ = PrintInfoTelegramAsync(stringBuilder.ToString(), CancellationToken.None);
                }
                else
                {
                    _ = PrintInfoTelegramAsync($"{type} #{bracketOrderFill.Index} bracket order '{bracketOrderFill.ClientOrderId}' of live bracketed order '{
                        bracketOrderFill.Order}' has been filled completely.", CancellationToken.None);
                }

                break;
            }

            case BracketOrderChanged bracketOrderChanged:
            {
                string type = bracketOrderChanged.BracketOrderType == BracketOrderType.StopLoss ? "Stop-loss" : "Take-profit";
                _ = PrintInfoTelegramAsync($"{type} #{bracketOrderChanged.Index} bracket order '{bracketOrderChanged.PreviousClientOrderId} with size {
                    bracketOrderChanged.PreviousBaseSize} has been replaced with bracket order '{bracketOrderChanged.NewClientOrderId}' with size {
                    bracketOrderChanged.NewBaseSize} for live bracketed order '{bracketOrderChanged.Order}'.", CancellationToken.None);
                break;
            }

            case BracketOrderCanceled bracketOrderCanceled:
            {
                string type = bracketOrderCanceled.BracketOrderType == BracketOrderType.StopLoss ? "Stop-loss" : "Take-profit";
                _ = PrintInfoTelegramAsync($"{type} #{bracketOrderCanceled.Index} bracket order '{bracketOrderCanceled.ClientOrderId} was canceled.", CancellationToken.None);
                break;
            }

            case ClosePositionOrderCreated closePositionOrderCreated:
                _ = PrintInfoTelegramAsync($"Close-position order '{closePositionOrderCreated.ClientOrderId}' of live bracketed order '{
                    closePositionOrderCreated.Order}' has been created.", CancellationToken.None);
                break;

            case ClosePositionOrderFill closePositionOrderFill:
            {
                StringBuilder stringBuilder = new();

                if (closePositionOrderFill.Fills.Count > 0)
                {
                    string msg = $"Close-position order '{closePositionOrderFill.ClientOrderId}' of live bracketed order '{closePositionOrderFill.Order}' has been filled:";

                    _ = stringBuilder
                        .AppendLine(msg)
                        .AppendLine("<code>");

                    foreach (FillData fillData in closePositionOrderFill.Fills)
                        _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  {fillData}");

                    _ = stringBuilder.AppendLine("</code>");

                    _ = PrintInfoTelegramAsync(stringBuilder.ToString(), CancellationToken.None);
                }
                else
                {
                    _ = PrintInfoTelegramAsync($"Close-position order '{closePositionOrderFill.ClientOrderId}' of live bracketed order '{
                        closePositionOrderFill.Order}' has been filled completely.", CancellationToken.None);
                }

                break;
            }
        }

        clog.Debug("$");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates bracket order definitions for the bracketed order.
    /// </summary>
    /// <param name="orderSide">Side of the working order.</param>
    /// <param name="lastPrice">Price of the last trade.</param>
    /// <param name="currentAtr">Current ATR value.</param>
    /// <param name="parameters">Program parameters.</param>
    /// <returns>List of bracket orders.</returns>
    private static BracketOrderDefinition[] CreateBracketOrdersDefinitions(OrderSide orderSide, decimal lastPrice, decimal currentAtr, Parameters parameters)
    {
        clog.Debug($"* {nameof(orderSide)}={orderSide},{nameof(lastPrice)}={lastPrice},{nameof(currentAtr)}={currentAtr},{nameof(parameters)}='{parameters}'");

        BracketOrderDefinition[] bracketOrdersDefinitions = new BracketOrderDefinition[parameters.StopLossCount + parameters.TakeProfitCount];

        int index = 0;
        if (parameters.StopLossCount > 0)
        {
            decimal slPercent = Math.Round(parameters.StopLossCount > 0 ? 100.0m / parameters.StopLossCount : 0, decimals: 2);
            decimal slPercentRemaining = 100.0m;
            decimal slThresholdPrice = orderSide == OrderSide.Buy ? lastPrice - (currentAtr * parameters.FirstStopLossAtr) : lastPrice + (currentAtr * parameters.FirstStopLossAtr);

            bracketOrdersDefinitions[index] = new(BracketOrderType.StopLoss, thresholdPrice: slThresholdPrice, sizePercent: slPercent);
            index++;

            slPercentRemaining -= slPercent;

            for (int i = 1; i < parameters.StopLossCount; i++)
            {
                slThresholdPrice = orderSide == OrderSide.Buy
                    ? slThresholdPrice - (currentAtr * parameters.NextStopLossAtrIncrement)
                    : slThresholdPrice + (currentAtr * parameters.NextStopLossAtrIncrement);

                decimal sizePercent = index == parameters.StopLossCount - 1 ? slPercentRemaining : slPercent;
                bracketOrdersDefinitions[index] = new(BracketOrderType.StopLoss, thresholdPrice: slThresholdPrice, sizePercent: sizePercent);
                index++;

                slPercentRemaining -= sizePercent;
            }
        }

        if (parameters.TakeProfitCount > 0)
        {
            decimal tpPercent = Math.Round(parameters.TakeProfitCount > 0 ? 100.0m / parameters.TakeProfitCount : 0, decimals: 2);
            decimal tpPercentRemaining = 100.0m;
            decimal tpThresholdPrice = orderSide == OrderSide.Buy
                ? lastPrice + (currentAtr * parameters.FirstTakeProfitAtr)
                : lastPrice - (currentAtr * parameters.FirstTakeProfitAtr);

            bracketOrdersDefinitions[index] = new(BracketOrderType.TakeProfit, thresholdPrice: tpThresholdPrice, sizePercent: tpPercent);
            index++;

            tpPercentRemaining -= tpPercent;

            for (int i = 1; i < parameters.TakeProfitCount; i++)
            {
                tpThresholdPrice = orderSide == OrderSide.Buy
                    ? tpThresholdPrice + (currentAtr * parameters.NextTakeProfitAtrIncrement)
                    : tpThresholdPrice - (currentAtr * parameters.NextTakeProfitAtrIncrement);

                decimal sizePercent = i == parameters.TakeProfitCount - 1 ? tpPercentRemaining : tpPercent;
                bracketOrdersDefinitions[index] = new(BracketOrderType.TakeProfit, thresholdPrice: tpThresholdPrice, sizePercent: sizePercent);
                index++;

                tpPercentRemaining -= sizePercent;
            }
        }

        clog.Debug($"$={bracketOrdersDefinitions.LogJoin()}");
        return bracketOrdersDefinitions;
    }

    /// <summary>
    /// Background task that creates report with the provided frequency.
    /// </summary>
    /// <param name="reportFilePath">Name of the file to generate reports.</param>
    /// <param name="tradeClient">Connected client.</param>
    /// <param name="reportPeriod">Time period to generate the first report and between generating reports.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task RunReportTaskAsync(string reportFilePath, ITradeApiClient tradeClient, TimeSpan reportPeriod, CancellationToken cancellationToken)
    {
        using IDisposable mdlc = clog.SetMdlc();
        clog.Debug($"* {nameof(reportFilePath)}='{reportFilePath}',{nameof(tradeClient)}={tradeClient},{nameof(reportPeriod)}={reportPeriod}");

        DateTime nextReport = DateTime.UtcNow.Add(reportPeriod);

        // All budget reports that have been generated during the program's lifetime.
        List<BudgetReport> budgetReports = new();

        try
        {
            while (true)
            {
                DateTime time = DateTime.UtcNow;

                if (time >= nextReport)
                {
                    await PrintInfoTelegramAsync($"Generating budget report ...", cancellationToken).ConfigureAwait(false);

                    try
                    {
                        await GenerateReportAsync(reportFilePath, tradeClient, budgetReports, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        string encodedException = HttpUtility.HtmlEncode(e.ToString());
                        await PrintErrorTelegramAsync($"Exception occurred while trying to generate report: {encodedException}.", cancellationToken).ConfigureAwait(false);
                    }

                    nextReport = time.Add(reportPeriod);
                    await PrintInfoTelegramAsync($"Next budget report should be generated at {nextReport:yyyy-MM-dd HH:mm:ss} UTC.", cancellationToken).ConfigureAwait(false);
                }

                TimeSpan delay = nextReport - time;

                if (delay > TimeSpan.Zero)
                {
                    PrintInfo($"Waiting {delay} before generating the next budget report.");

                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        clog.Debug("Cancelation detected.");
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            throw new SanityCheckException($"Exception occurred in the report task: {e}");
        }

        clog.Debug("$");
    }

    /// <summary>
    /// Background task that processes termination of live bracketed orders. After the shutdown is detected, all positions that are still open are closed. This method also cares
    /// about disposing live bracketed orders when they terminate.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task RunBracketedOrderTerminationMonitoringTaskAsync(CancellationToken cancellationToken)
    {
        using IDisposable mdlc = clog.SetMdlc();
        clog.Debug("*");

        try
        {
            List<Task> tasks = new();
            List<ILiveBracketedOrder> ordersToRemove = new();
            Dictionary<ILiveBracketedOrder, Task> mapCopy = new();
            Task newOrderTask = newLiveBracketedOrder.WaitAsync(cancellationToken);

            try
            {
                while (true)
                {
                    tasks.Clear();
                    ordersToRemove.Clear();
                    mapCopy.Clear();
                    lock (liveLock)
                    {
                        tasks.AddRange(liveBracketedOrdersTerminationTasksMap.Values);

                        foreach ((ILiveBracketedOrder liveBracketedOrder, Task terminationTask) in liveBracketedOrdersTerminationTasksMap)
                            mapCopy.Add(liveBracketedOrder, terminationTask);
                    }

                    tasks.Add(newOrderTask);

                    _ = await Task.WhenAny(tasks).ConfigureAwait(false);

                    foreach ((ILiveBracketedOrder liveBracketedOrder, Task terminationTask) in mapCopy)
                    {
                        if (terminationTask.IsCompleted)
                        {
                            await terminationTask.ConfigureAwait(false);
                            ordersToRemove.Add(liveBracketedOrder);

                            clog.Debug($"Disposing live bracketed order '{liveBracketedOrder}' after it terminated.");
                            await liveBracketedOrder.DisposeAsync().ConfigureAwait(false);
                        }
                    }

                    if (ordersToRemove.Count > 0)
                    {
                        StringBuilder stringBuilder = new();
                        lock (liveLock)
                        {
                            foreach (ILiveBracketedOrder liveBracketedOrder in ordersToRemove)
                            {
                                _ = liveBracketedOrdersTerminationTasksMap.Remove(liveBracketedOrder);
                                clog.Debug($"Live bracketed order '{liveBracketedOrder}' has been removed from the map.");

                                openPositions--;
                                _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture,
                                    $"Live bracketed order '{liveBracketedOrder}' has been completed. There are now {openPositions} open positions.");
                            }
                        }

                        await PrintInfoTelegramAsync(stringBuilder.ToString(), cancellationToken).ConfigureAwait(false);
                    }

                    if (newOrderTask.IsCompleted)
                    {
                        // This throws in case of shutdown.
                        await newOrderTask.ConfigureAwait(false);

                        // Refresh the task if it was not canceled.
                        newOrderTask = newLiveBracketedOrder.WaitAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                clog.Debug("Shutdown detected.");
            }

            mapCopy.Clear();
            lock (liveLock)
            {
                clog.Debug($"There are {liveBracketedOrdersTerminationTasksMap.Count} live orders.");

                foreach ((ILiveBracketedOrder liveBracketedOrder, Task terminationTask) in liveBracketedOrdersTerminationTasksMap)
                    mapCopy.Add(liveBracketedOrder, terminationTask);

                liveBracketedOrdersTerminationTasksMap.Clear();
            }

            foreach ((ILiveBracketedOrder liveBracketedOrder, Task terminationTask) in mapCopy)
            {
                clog.Debug($"Closing position of bracketed order '{liveBracketedOrder}'.");
                await liveBracketedOrder.ClosePositionAsync(waitForClosePositionFill: true, CancellationToken.None).ConfigureAwait(false);

                clog.Debug($"Disposing live bracketed order '{liveBracketedOrder}' after its position has been closed.");
                await liveBracketedOrder.DisposeAsync().ConfigureAwait(false);

                clog.Debug($"Waiting for the live bracketed order '{liveBracketedOrder}' to be terminated.");
                await terminationTask.ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            throw new SanityCheckException($"Exception occurred in the bracketed order termination monitoring task: {e}");
        }

        clog.Debug("$");
    }

    /// <summary>
    /// Prints bot's settings to the log, console, and to the Telegram.
    /// </summary>
    /// <param name="parameters">Bot's parameters.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PrintIntroAsync(Parameters parameters, CancellationToken cancellationToken)
    {
        clog.Debug($"* {nameof(parameters)}='{parameters}'");

        await PrintInfoTelegramAsync($$"""
            Bot started with parameters:
            <pre>
            {{parameters}}
            </pre>
            """, cancellationToken).ConfigureAwait(false);

        StringBuilder stringBuilder = new();
        _ = stringBuilder
            .AppendLine("Current budget:")
            .AppendLine();

        foreach ((string assetName, decimal amount) in parameters.BudgetRequest.InitialBudget)
            _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $" {assetName}: {amount}");

        string initialBudget = stringBuilder.ToString();
        await PrintInfoTelegramAsync($"Initial budget: {initialBudget}", cancellationToken).ConfigureAwait(false);

        clog.Debug("$");
    }

    /// <summary>
    /// Generates report and writes its to the file, to the console, and to the log.
    /// </summary>
    /// <param name="reportFilePath">Full path to the report file.</param>
    /// <param name="tradeClient">Connected client.</param>
    /// <param name="budgetReports">List of all budget reports that have been generated during the program's lifetime.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
    /// <exception cref="OperationFailedException">Thrown if it was not possible to calculate the value of the budget or write the report to a file.</exception>
    private static async Task GenerateReportAsync(string reportFilePath, ITradeApiClient tradeClient, List<BudgetReport> budgetReports, CancellationToken cancellationToken)
    {
        clog.Debug($" {nameof(reportFilePath)}='{reportFilePath}',{nameof(tradeClient)}='{tradeClient}'");

        BudgetReport budgetReport = await tradeClient.GenerateBudgetReportAsync(cancellationToken).ConfigureAwait(false);
        string reportLog = Reports.BudgetReportToString(budgetReport);
        await PrintInfoTelegramAsync(reportLog, cancellationToken).ConfigureAwait(false);

        await ReportToFileAsync(reportFilePath, budgetReport, budgetReports).ConfigureAwait(false);

        clog.Debug("$");
    }

    /// <summary>
    /// Writes the report to the file.
    /// </summary>
    /// <param name="reportFilePath">Full path to the report file.</param>
    /// <param name="budgetReport">Latest budget to write to the file.</param>
    /// <param name="budgetReports">List of all budget reports that have been generated during the program's lifetime.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Since it is possible that the initial budget request does not contain all assets, the list of assets can change over time. Therefore, we have to always write all reports
    /// to the file from scratch, to make sure all columns match with the header.
    /// </remarks>
    /// <exception cref="OperationFailedException">Thrown if writing the report to a file failed.</exception>
    private static async Task ReportToFileAsync(string reportFilePath, BudgetReport budgetReport, List<BudgetReport> budgetReports)
    {
        clog.Debug($" {nameof(reportFilePath)}='{reportFilePath}',{nameof(budgetReport)}='{budgetReport}'");

        budgetReports.Add(budgetReport);

        string budgetReportsCsv = Reports.BudgetReportsToCsvString(budgetReports);

        try
        {
            // No cancellation token here to avoid losing data in case user presses Ctrl+C at the time of writing.
            await File.WriteAllTextAsync(reportFilePath, budgetReportsCsv, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            clog.Error($"Exception occurred while writing reports to the report file '{reportFilePath}': {e}");

            throw new OperationFailedException($"Writing reports to the report file '{reportFilePath}' failed.", e);
        }

        clog.Debug("$");
    }

    /// <summary>
    /// Converts Whale's Secret candle representation to OHLCV data format for <see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see>.
    /// </summary>
    /// <param name="candle">Whale's Secret candle to convert.</param>
    /// <returns><see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see> quote representing the candle.</returns>
    private static Quote QuoteFromCandle(Candle candle)
    {
        Quote quote = new()
        {
            Date = candle.Timestamp,
            Open = candle.OpenPrice,
            High = candle.HighPrice,
            Low = candle.LowPrice,
            Close = candle.ClosePrice,
            Volume = candle.BaseVolume,
        };

        return quote;
    }

    /// <summary>
    /// Converts <see cref="CandleWidth"/> to <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="candleWidth">Candle width to convert.</param>
    /// <param name="timeSpan">
    /// If the function succeeds, this is filled with the time span that corresponds to the input candle width. All candle widths have their precise corresponding time span except
    /// for <see cref="CandleWidth.Month1"/>, which is defined as <c>31</c> days long time span. This is to make sure that the returned time span is the longest possible time span
    /// for the input candle width.
    /// </param>
    /// <returns>
    /// <c>true</c> if the conversion is possible, <c>false</c> otherwise. The only case when <c>false</c> is returned is when the input candle width is set to
    /// <see cref="CandleWidth.Other"/>.
    /// </returns>
    public static bool CandleWidthToTimeSpan(CandleWidth candleWidth, [NotNullWhen(true)] out TimeSpan? timeSpan)
    {
        timeSpan = candleWidth switch
        {
            CandleWidth.Minute1 => TimeSpan.FromMinutes(1),
            CandleWidth.Minutes3 => TimeSpan.FromMinutes(3),
            CandleWidth.Minutes5 => TimeSpan.FromMinutes(5),
            CandleWidth.Minutes15 => TimeSpan.FromMinutes(15),
            CandleWidth.Minutes30 => TimeSpan.FromMinutes(30),
            CandleWidth.Hour1 => TimeSpan.FromHours(1),
            CandleWidth.Hours2 => TimeSpan.FromHours(2),
            CandleWidth.Hours4 => TimeSpan.FromHours(4),
            CandleWidth.Hours6 => TimeSpan.FromHours(6),
            CandleWidth.Hours8 => TimeSpan.FromHours(8),
            CandleWidth.Hours12 => TimeSpan.FromHours(12),
            CandleWidth.Day1 => TimeSpan.FromDays(1),
            CandleWidth.Days3 => TimeSpan.FromDays(3),
            CandleWidth.Week1 => TimeSpan.FromDays(7),
            CandleWidth.Month1 => TimeSpan.FromDays(31),
            _ => null,
        };

        return timeSpan is not null;
    }

    /// <inheritdoc cref="ConnectionOptions.OnConnectedDelegateAsync"/>
    private static async Task OnConnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        PrintInfo();
        await PrintInfoTelegramAsync("Connection to the exchange has been re-established successfully.", CancellationToken.None).ConfigureAwait(false);
        PrintInfo();
    }

    /// <inheritdoc cref="ConnectionOptions.OnDisconnectedDelegateAsync"/>
    private static async Task OnDisconnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        PrintInfo();
        await PrintInfoTelegramAsync("CONNETION TO THE EXCHANGE HAS BEEN INTERRUPTED!!", CancellationToken.None).ConfigureAwait(false);
        PrintInfo();
    }
}