using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.SharedLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.DCA;

/// <summary>
/// DCA (Direct Cost Averaging) trading bot. This bot periodically places market orders in order to buy (or sell) the selected base asset for the constant amount of the selected
/// quote asset.
/// <para>
/// For example, if the symbol pair is <c>BTC/EUR</c>, the quote size is <c>10</c>, and the period is <c>3600</c> seconds, the bot will try to buy (or sell) 10 <c>EUR</c> worth of
/// BTC every hour.
/// </para>
/// <para>The bot also create reports about its performance and writes the report history it into a CSV file.</para>
/// </summary>
internal class Program
{
    /// <summary>Name of the trading strategy.</summary>
    private const string StrategyName = "DCA";

    /// <summary>Name of the report file.</summary>
    private const string ReportFileName = $"{StrategyName}-budgetReport.csv";

    /// <summary>All budget reports that have been generated during the program's lifetime.</summary>
    private static readonly List<BudgetReport> budgetReports = new();

    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

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
    ///   "Period": "00:00:10",
    ///   "QuoteSize": 10.0,
    ///   "OrderSide": "Buy",
    ///   "BudgetRequest": {
    ///     "StrategyName": "DCA",
    ///     "PrimaryAsset": "EUR",
    ///     "InitialBudget": {
    ///       "EUR": 1000,
    ///       "BTC": 0.001
    ///     }
    ///   },
    ///   "ReportPeriod": "00:00:30"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// With this input the program will buy <c>10</c> EUR worth of BTC every hour on Binance exchange. The report will be generated every 24 hours. And the initial budget is
    /// <c>0.001</c> BTC and <c>1000</c> EUR.
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
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(Samples)}}.{{nameof(AdvancedDemos)}}.{{nameof(DCA)}} <parametersFilePath>
                """);

            clog.Info("$<USAGE>");
            clog.FlushAndShutDown();

            return;
        }

        string parametersFilePath = args[0];
        Parameters parameters = Parameters.LoadFromJson(parametersFilePath);

        PrintInfo("Press Ctrl+C to terminate the program.");
        PrintInfo();

        PrintInfo($"Starting DCA on {parameters.ExchangeMarket}, {parameters.OrderSide}ing {parameters.QuoteSize} {parameters.SymbolPair.QuoteSymbol} worth of {parameters.SymbolPair.BaseSymbol} every {parameters.Period}. Reports will be generated every {parameters.ReportPeriod}.");
        PrintInfo($"Budget request: {parameters.BudgetRequest}");
        PrintInfo();

        using CancellationTokenSource shutdownCancellationTokenSource = new();
        CancellationToken shutdownToken = shutdownCancellationTokenSource.Token;

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
            await StartDcaAsync(parameters, shutdownToken).ConfigureAwait(false);
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
    /// Prints information level message to the console and to the log. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="msg">Message to print.</param>
    private static void PrintInfo(string msg = "")
    {
        clog.Info(msg);

        if (msg.Length > 0)
        {
            DateTime dateTime = DateTime.UtcNow;
            string dateTimeStr = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Console.WriteLine($"{dateTimeStr}: {msg}");
        }
        else Console.WriteLine();
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
    private static async Task StartDcaAsync(Parameters parameters, CancellationToken cancellationToken)
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

        PrintInfo($"Connect to {parameters.ExchangeMarket} exchange with full-trading access.");

        ConnectionOptions connectionOptions = new(BlockUntilReconnectedOrTimeout.InfinityTimeoutInstance, ConnectionType.FullTrading, OnConnectedAsync, OnDisconnectedAsync,
            budgetRequest: parameters.BudgetRequest);
        ITradeApiClient tradeClient = await scriptApi.ConnectAsync(parameters.ExchangeMarket, connectionOptions).ConfigureAwait(false);

        PrintInfo($"Connection to {parameters.ExchangeMarket} has been established successfully.");

        OrderRequestBuilder<MarketOrderRequest> builder = tradeClient.CreateOrderRequestBuilder<MarketOrderRequest>();
        _ = builder
            .SetSymbolPair(parameters.SymbolPair)
            .SetSide(OrderSide.Buy)
            .SetSizeInBaseSymbol(sizeInBaseSymbol: false)
            .SetSize(parameters.QuoteSize);

        int orderCounter = 0;

        DateTime nextOrder = DateTime.MinValue;
        DateTime nextReport = DateTime.UtcNow.Add(parameters.ReportPeriod);

        string reportFilePath = Path.Combine(parameters.AppDataPath, ReportFileName);

        while (true)
        {
            DateTime time = DateTime.UtcNow;
            if (time >= nextOrder)
            {
                orderCounter++;

                MarketOrderRequest orderRequest = builder
                    .SetClientOrderId($"dca_{orderCounter:00000000}{ITradingStrategyBudget.ClientOrderIdSuffix}")
                    .Build();

                await PlaceOrderAsync(tradeClient, orderRequest, cancellationToken).ConfigureAwait(false);

                nextOrder = time.Add(parameters.Period);
                PrintInfo($"Next order should be placed at {nextOrder:yyyy-MM-dd HH:mm:ss} UTC.");
            }

            time = DateTime.UtcNow;
            if (time >= nextReport)
            {
                PrintInfo($"Generating budget report ...");
                await GenerateReportAsync(reportFilePath, tradeClient, cancellationToken).ConfigureAwait(false);

                nextReport = time.Add(parameters.ReportPeriod);
                PrintInfo($"Next budget report should be generated at {nextReport:yyyy-MM-dd HH:mm:ss} UTC.");
            }

            time = DateTime.UtcNow;
            TimeSpan delayTillOrder = nextOrder - time;
            TimeSpan delayTillReport = nextReport - time;
            TimeSpan delay = delayTillOrder < delayTillReport ? delayTillOrder : delayTillReport;

            if (delay > TimeSpan.Zero)
            {
                if (delay == delayTillOrder) PrintInfo($"Waiting {delay} before placing the next order.");
                else PrintInfo($"Waiting {delay} before generating the next budget report.");

                try
                {
                    await Task.Delay(parameters.Period, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    clog.Debug("Cancelation detected.");

                    clog.Debug("$");
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Places the order request to the exchange and waits for the order to be filled.
    /// </summary>
    /// <param name="tradeClient">Connected client.</param>
    /// <param name="orderRequest">Request for the order to place.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="BudgetCalculationException">Thrown if a trading strategy budget is associated with the trade API client and it is not possible to calculate whether
    /// an order would exceed it if it was placed.</exception>
    /// <exception cref="BudgetExceededException">Thrown if a trading strategy budget is associated with the trade API client and placing the order would exceed the budget.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled including cancellation due to shutdown or object disposal.</exception>
    /// <exception cref="OperationFailedException">Thrown if the request could not be sent to the exchange.</exception>
    private static async Task PlaceOrderAsync(ITradeApiClient tradeClient, MarketOrderRequest orderRequest, CancellationToken cancellationToken)
    {
        clog.Debug($" {nameof(tradeClient)}='{tradeClient}',{nameof(orderRequest)}='{orderRequest}'");

        try
        {
            ILiveMarketOrder order = await tradeClient.CreateOrderAsync(orderRequest, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<FillData> fillData = await order.WaitForFillAsync(cancellationToken).ConfigureAwait(false);

            PrintInfo($"Order client ID '{order.ClientOrderId}' has been filled with {fillData.Count} trade(s).");
        }
        catch (Exception e)
        {
            if ((e is OperationCanceledException) || (e is BudgetExceededException) || (e is BudgetCalculationException))
                throw;

            throw new OperationFailedException($"Placing order request '{orderRequest}' on the exchange failed.", e);
        }

        clog.Debug("$");
    }

    /// <summary>
    /// Generates report and writes its to the file, to the console, and to the log.
    /// </summary>
    /// <param name="reportFilePath">Full path to the report file.</param>
    /// <param name="tradeClient">Connected client.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
    /// <exception cref="OperationFailedException">Thrown if it was not possible to calculate the value of the budget or write the report to a file.</exception>
    private static async Task GenerateReportAsync(string reportFilePath, ITradeApiClient tradeClient, CancellationToken cancellationToken)
    {
        clog.Debug($" {nameof(reportFilePath)}='{reportFilePath}',{nameof(tradeClient)}='{tradeClient}'");

        BudgetReport budgetReport = await tradeClient.GenerateBudgetReportAsync(cancellationToken).ConfigureAwait(false);
        string reportLog = Reports.BudgetReportToString(budgetReport);
        PrintInfo(reportLog);

        await ReportToFileAsync(reportFilePath, budgetReport).ConfigureAwait(false);

        clog.Debug("$");
    }

    /// <summary>
    /// Writes the report to the file.
    /// </summary>
    /// <param name="reportFilePath">Full path to the report file.</param>
    /// <param name="budgetReport">Latest budget to write to the file.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Since it is possible that the initial budget request does not contain all assets, the list of assets can change over time. Therefore, we have to always write all reports
    /// to the file from scratch, to make sure all columns match with the header.
    /// </remarks>
    /// <exception cref="OperationFailedException">Thrown if writing the report to a file failed.</exception>
    private static async Task ReportToFileAsync(string reportFilePath, BudgetReport budgetReport)
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

    /// <inheritdoc cref="ConnectionOptions.OnConnectedDelegateAsync"/>
    private static Task OnConnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        PrintInfo();
        PrintInfo("Connection to the exchange has been re-established successfully.");
        PrintInfo();

        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ConnectionOptions.OnDisconnectedDelegateAsync"/>
    private static Task OnDisconnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        PrintInfo();
        PrintInfo("CONNETION TO THE EXCHANGE HAS BEEN INTERRUPTED!!");
        PrintInfo();

        return Task.CompletedTask;
    }
}