using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;
using WhalesSecret.TradeScriptLib.Utils.Orders;

namespace WhalesSecret.ScriptApiLib.DCA;

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

    /// <summary>Separator of values on a single row in the report file.</summary>
    private const char ReportFileValueSeparator = ',';

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
    ///   "AppDataFolder": "Data",
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
    /// With this ipnut the program will will buy <c>10</c> EUR worth of BTC every hour on Binance exchange. The report will be generated every 24 hours. And the initial budget is
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
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(DCA)}} <parametersFilePath>
                """);

            clog.Info("$<USAGE>");
            clog.FlushAndShutDown();

            return;
        }

        string parametersFilePath = args[0];
        Parameters parameters = Parameters.LoadFromJson(parametersFilePath);

        PrintInfo("Press Ctrl+C to terminate the program.");
        PrintInfo();

        PrintInfo($"Starting DCA on {parameters.ExchangeMarket}, {parameters.OrderSide}ing {parameters.QuoteSize} {parameters.SymbolPair.QuoteSymbol} worth of {
            parameters.SymbolPair.BaseSymbol} every {parameters.Period}. Reports will be generated every {parameters.ReportPeriod}.");
        PrintInfo($"Budget request: {parameters.BudgetRequest}");
        PrintInfo();

        using CancellationTokenSource shutdownCancellationTokenSource = new();
        CancellationToken shutdownToken = shutdownCancellationTokenSource.Token;

        // Install Ctrl+C / SIGINT handler.
        ConsoleCancelEventHandler controlCancelHandler = (object? sender, ConsoleCancelEventArgs e) =>
        {
            clog.Info("[CCEH] *");

            // If cancellation of the control event is set to true, the process won't terminate automatically and we will have a control over the shutdown.
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
    /// Parses input budget string from the command line.
    /// </summary>
    /// <param name="str">String to parse.</param>
    /// <param name="budgetRequest">If the function succeeds, this is filled with the parsed configuration.</param>
    /// <returns><c>true</c> if parsing was successful, <c>false</c> if the format of the string to parse was invalid.</returns>
    private static bool TryParseBudget(string str, [NotNullWhen(true)] out BudgetRequest? budgetRequest)
    {
        clog.Debug($"* {nameof(str)}='{str}'");

        bool result = false;
        budgetRequest = null;

        string[] assetAllocations = str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (assetAllocations.Length > 0)
        {
            bool error = false;

            string? primaryAsset = null;
            BudgetSnapshot initialBudget = new();

            foreach (string assetAllocation in assetAllocations)
            {
                string[] parts = assetAllocation.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length != 2)
                {
                    clog.Error($"'{assetAllocation}' has invalid format, expected \"assetName=value\".");
                    error = true;
                    break;
                }

                string assetName = parts[0];
                string amountStr = parts[1];

                if (primaryAsset is null)
                    primaryAsset = assetName;

                if (!decimal.TryParse(amountStr, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal amount))
                {
                    clog.Error($"'{amountStr}' is not a valid decimal number.");
                    error = true;
                    break;
                }

                if (!initialBudget.TryAdd(assetName, amount))
                {
                    clog.Error($"'{assetName}' is present more than once.");
                    error = true;
                    break;
                }
            }

            if (!error && (primaryAsset is not null))
            {
                budgetRequest = new(StrategyName, primaryAsset, initialBudget);
                result = true;
            }
        }
        else clog.Error("Budget is empty.");

        clog.Debug($"$={result},{nameof(budgetRequest)}='{budgetRequest}'");
        return result;
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
        CreateOptions createOptions = new(appDataFolder: parameters.AppDataFolder, license: License.WsLicense);
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

        string reportFilePath = Path.Combine(parameters.AppDataFolder, ReportFileName);

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
                PrintInfo($"Next budgetReport should be generated at {nextReport:yyyy-MM-dd HH:mm:ss} UTC.");
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

        string initialValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.InitialValue}");
        string finalValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.FinalValue}");
        string totalProfitStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.TotalProfit}");
        string totalFeesValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.TotalFeesValue}");

        string reportLog = $$"""
            Budget report:
              start time: {{budgetReport.StartTime}} UTC
              end time: {{budgetReport.EndTime}} UTC
              initial value: {{initialValueStr}} {{budgetReport.PrimaryAsset}}
              final value: {{finalValueStr}} {{budgetReport.PrimaryAsset}}
              profit/loss: {{totalProfitStr}} {{budgetReport.PrimaryAsset}}
              fees value paid: {{totalFeesValueStr}} {{budgetReport.PrimaryAsset}}
            """;

        PrintInfo(reportLog);

        StringBuilder stringBuilder = new("Current budget:");
        _ = stringBuilder.AppendLine();

        foreach ((string assetName, decimal amount) in budgetReport.FinalBudget)
            _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $" {assetName}: {amount}");

        _ = stringBuilder.AppendLine();

        string currentBudgetLog = stringBuilder.ToString();
        PrintInfo(currentBudgetLog);

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

        StringBuilder fileContentBuilder = new();
        string primaryAsset = budgetReport.PrimaryAsset;

        // Compose the header from the latest report.
        _ = fileContentBuilder
            .Append("Report Date Time (UTC)")
            .Append(ReportFileValueSeparator)
            .Append("Total Report Period")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"Value ({primaryAsset})")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"Diff last report ({primaryAsset})")
            .Append(ReportFileValueSeparator)
            .Append(CultureInfo.InvariantCulture, $"P/L ({primaryAsset})")
            .Append(ReportFileValueSeparator);

        string[] assetNames = budgetReport.FinalBudget.Keys.Order().ToArray();

        for (int i = 0; i < assetNames.Length; i++)
        {
            _ = fileContentBuilder
                .Append(CultureInfo.InvariantCulture, $"Budget Balance {assetNames[i]}")
                .Append(ReportFileValueSeparator);
        }

        string[] feeAssetNames = budgetReport.FeesPaid.Keys.Order().ToArray();

        for (int i = 0; i < feeAssetNames.Length; i++)
        {
            _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"Fees Paid {assetNames[i]}");

            if (i != feeAssetNames.Length - 1)
                _ = fileContentBuilder.Append(ReportFileValueSeparator);
        }

        _ = fileContentBuilder.AppendLine();

        decimal prevValue = 0;

        for (int i = -1; i < budgetReports.Count; i++)
        {
            BudgetSnapshot snapshot;
            BudgetSnapshot feesPaid;

            if (i == -1)
            {
                // Second row is the initial budget line
                _ = fileContentBuilder
                    .Append(budgetReport.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(ReportFileValueSeparator)
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{budgetReport.InitialValue}")
                    .Append(ReportFileValueSeparator)
                    .Append('0')
                    .Append(ReportFileValueSeparator)
                    .Append('0')
                    .Append(ReportFileValueSeparator);

                snapshot = budgetReport.InitialBudget;
                feesPaid = new();

                prevValue = budgetReport.InitialValue;
            }
            else
            {
                BudgetReport report = budgetReports[i];
                TimeSpan period = report.EndTime - budgetReport.StartTime;
                decimal diff = report.FinalValue - prevValue;

                _ = fileContentBuilder
                    .Append(report.EndTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(ReportFileValueSeparator)
                    .Append(period.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture))
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{report.FinalValue}")
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{diff}")
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{report.TotalProfit}")
                    .Append(ReportFileValueSeparator);

                snapshot = report.FinalBudget;
                feesPaid = report.FeesPaid;

                prevValue = report.FinalValue;
            }

            for (int assetNameIndex = 0; assetNameIndex < assetNames.Length; assetNameIndex++)
            {
                string assetName = assetNames[assetNameIndex];

                if (snapshot.TryGetValue(assetName, out decimal value))
                    _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"{value}");

                _ = fileContentBuilder.Append(ReportFileValueSeparator);
            }

            for (int feeAssetNameIndex = 0; feeAssetNameIndex < feeAssetNames.Length; feeAssetNameIndex++)
            {
                string assetName = feeAssetNames[feeAssetNameIndex];

                if (feesPaid.TryGetValue(assetName, out decimal value))
                    _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"{value}");

                if (feeAssetNameIndex != feeAssetNames.Length - 1)
                    _ = fileContentBuilder.Append(ReportFileValueSeparator);
            }

            _ = fileContentBuilder.AppendLine();
        }

        string fileContents = fileContentBuilder.ToString();

        try
        {
            // No cancellation token here to avoid losing data in case user presses Ctrl+C at the time of writing.
            await File.WriteAllTextAsync(reportFilePath, fileContents, CancellationToken.None).ConfigureAwait(false);
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