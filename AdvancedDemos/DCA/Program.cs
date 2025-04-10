using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
/// Main application class that contains program entry point.
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
    /// Application that fetches new ticker data and displays it.
    /// </summary>
    /// <param name="args">Command-line arguments.
    /// <para>The program must be started with 6 arguments given in this order:
    /// <list type="table">
    /// <item><c>exchangeMarket</c> – <see cref="ExchangeMarket">Exchange market</see> to DCA at.</item>
    /// <item><c>symbolPair</c> – Symbol pair to DCA, e.g. <c>BTC/EUR</c>.</item>
    /// <item><c>periodSeconds</c> – Time period in seconds between the orders, e.g. <c>3600</c> for 1 hour period. First order is placed just after start.</item>
    /// <item><c>quoteSize</c> – Order size in quote symbol, e.g. <c>10</c> to buy <c>10</c> EUR worth of BTC with each trade.</item>
    /// <item><c>budget</c> – Comma-separated list of budget allocations for each asset with the primary asset being first. Each budget allocation is a pair of asset name
    /// and the amount, separated by an equal sign. E.g. <c>EUR=100,BTC=0.1</c> would allocate budget with primary asset EUR that can has <c>100</c> EUR and <c>0.1</c> BTC
    /// available.</item>
    /// <item><c>reportPeriodSeconds</c> - Time period in seconds before the first report is generated and between reports are generated.</item>
    /// </list>
    /// </para>
    /// <para>Run the program without any arguments to see the supported values for each argument.</para>
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task Main(string[] args)
    {
        using IDisposable mdlc = clog.SetMdlc();

        clog.Info($"* {nameof(args)}={args.LogJoin()}");

        string? error = null;

        ExchangeMarket? exchangeMarket = null;
        SymbolPair? symbolPair = null;
        int? periodSeconds = null;
        decimal? quoteSize = null;
        BudgetRequest? budgetRequest = null;
        int? reportPeriodSeconds = null;

        if (args.Length == 6)
        {
            string exchangeMarketStr = args[0];
            string symbolPairStr = args[1];
            string periodSecondsStr = args[2];
            string quoteSizeStr = args[3];
            string budgetStr = args[4];
            string reportPeriodSecondsStr = args[5];

            if (!Enum.TryParse(exchangeMarketStr, out ExchangeMarket exchangeMarketParsed))
            {
                error = $"'{exchangeMarketStr}' is not a valid exchange market.";
            }
            else if (!SymbolPair.TryParseToString(symbolPairStr, out SymbolPair? symbolPairParsed))
            {
                error = $"'{symbolPairStr}' is not a valid symbol pair.";
            }
            else if (!int.TryParse(periodSecondsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int periodSecondsParsed))
            {
                error = $"'{periodSecondsStr}' is not a valid period in seconds.";
            }
            else if (!decimal.TryParse(quoteSizeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal quoteSizeParsed))
            {
                error = $"'{quoteSizeStr}' is not a valid quote size.";
            }
            else if (!TryParseBudget(budgetStr, out BudgetRequest? budgetRequestParsed))
            {
                error = $"'{budgetStr}' is not a valid budget description.";
            }
            else if (!int.TryParse(reportPeriodSecondsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int reportPeriodSecondsParsed))
            {
                error = $"'{reportPeriodSecondsStr}' is not a valid period in seconds.";
            }
            else
            {
                exchangeMarket = exchangeMarketParsed;
                symbolPair = symbolPairParsed;
                periodSeconds = periodSecondsParsed;
                quoteSize = quoteSizeParsed;
                budgetRequest = budgetRequestParsed;
                reportPeriodSeconds = reportPeriodSecondsParsed;
            }
        }

        if (error is not null)
        {
            await Console.Error.WriteLineAsync($$"""
                ERROR: {{error}}

                """).ConfigureAwait(false);
        }

        if ((exchangeMarket is null) || (symbolPair is null) || (periodSeconds is null) || (quoteSize is null) || (budgetRequest is null) || (reportPeriodSeconds is null))
        {
            string markets = string.Join(',', Enum.GetValues<ExchangeMarket>());

            await Console.Out.WriteLineAsync($$"""
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(DCA)}} <exchangeMarket> <symbolPair> <periodSeconds> <quoteSize> <budget> <reportPeriodSeconds>

                    exchangeMarket - Exchange market to use in the sample. Supported values are {{markets}}.
                    symbolPair - Symbol pair to DCA, e.g. "BTC/EUR".
                    periodSeconds - Time period in seconds between the orders, e.g. "3600" for 1 hour period. First order is placed just after start.
                    quoteSize - Order size in quote symbol, e.g. "10" to buy 10 EUR worth of BTC with each trade.
                    budget - Comma-separated list of budget allocations for each asset with the primary asset being first. Each budget allocation is a pair of asset name{{
                " "}}and the amount, separated by an equal sign. E.g. "EUR=100,BTC=0.1" would allocate budget with primary asset EUR that can has 100 EUR and 0.1 BTC{{
                " "}}available.
                    reportPeriodSeconds - Time period in seconds before the first budgetReport is generated and between reports are generated.
                """).ConfigureAwait(false);

            clog.Info($"$<USAGE>");
            clog.FlushAndShutDown();

            return;
        }

        await PrintInfoAsync("Press Ctrl+C to terminate the program.").ConfigureAwait(false);
        await PrintInfoAsync().ConfigureAwait(false);

        await PrintInfoAsync($"Starting DCA on {exchangeMarket}, buying {quoteSize} {symbolPair.Value.QuoteSymbol} worth of {symbolPair.Value.BaseSymbol} every {
            periodSeconds} seconds. Reports will be generated every {reportPeriodSeconds} seconds.").ConfigureAwait(false);
        await PrintInfoAsync($"Budget request: {budgetRequest}").ConfigureAwait(false);
        await PrintInfoAsync().ConfigureAwait(false);

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
            string appPath = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            string appDataFolder = Path.Combine(appPath, "Data");
            await StartDcaAsync(appDataFolder, exchangeMarket.Value, symbolPair.Value, period: TimeSpan.FromSeconds(periodSeconds.Value), quoteSize.Value, budgetRequest,
                reportPeriod: TimeSpan.FromSeconds(reportPeriodSeconds.Value), shutdownToken).ConfigureAwait(false);
        }
        finally
        {
            // Uninstall Ctrl+C / SIGINT handler.
            Console.CancelKeyPress -= controlCancelHandler;
        }

        clog.Info($"$");
        clog.FlushAndShutDown();
    }

    /// <summary>
    /// Prints information level message to the console and to the log. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="msg">Message to print.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PrintInfoAsync(string msg = "")
    {
        clog.Info(msg);

        if (msg.Length > 0)
        {
            DateTime dateTime = DateTime.UtcNow;
            string dateTimeStr = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            await Console.Out.WriteLineAsync($"{dateTimeStr}: {msg}").ConfigureAwait(false);
        }
        else await Console.Out.WriteLineAsync().ConfigureAwait(false);
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
    /// <param name="appDataFolder">Path to the application data folder.</param>
    /// <param name="exchangeMarket">Exchange market to DCA at.</param>
    /// <param name="symbolPair">Symbol pair to DCA.</param>
    /// <param name="period">Time period in between the orders.</param>
    /// <param name="quoteSize">Order size in quote symbol.</param>
    /// <param name="budgetRequest">Description of budget parameters for the trading strategy.</param>
    /// <param name="reportPeriod">Time period to generate the first report and between generating reports.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task StartDcaAsync(string appDataFolder, ExchangeMarket exchangeMarket, SymbolPair symbolPair, TimeSpan period, decimal quoteSize,
        BudgetRequest budgetRequest, TimeSpan reportPeriod, CancellationToken cancellationToken)
    {
        clog.Debug($"* {nameof(appDataFolder)}='{appDataFolder}',{nameof(exchangeMarket)}={exchangeMarket},{nameof(symbolPair)}='{symbolPair}',{nameof(period)}={period},{
            nameof(quoteSize)}={quoteSize},{nameof(budgetRequest)}='{budgetRequest}',{nameof(reportPeriod)}={reportPeriod}");

        // In order to unlock large orders, a valid license has to be used.
        CreateOptions createOptions = new(appDataFolder: appDataFolder, connectToBinanceSandbox: true, license: License.WsLicense);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2000 // Dispose objects before losing scope
        IApiIdentity apiIdentity = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => Credentials.GetBinanceHmacApiIdentity(),
            ExchangeMarket.KucoinSpot => Credentials.GetKucoinApiIdentity(),
            _ => throw new SanityCheckException($"Unsupported exchange market {exchangeMarket} provided."),
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        scriptApi.SetCredentials(apiIdentity);

        await PrintInfoAsync($"Connect to {exchangeMarket} exchange with full-trading access.").ConfigureAwait(false);

        ConnectionOptions connectionOptions = new(BlockUntilReconnectedOrTimeout.InfinityTimeoutInstance, ConnectionType.FullTrading, OnConnectedAsync, OnDisconnectedAsync,
            budgetRequest: budgetRequest);
        ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await PrintInfoAsync($"Connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        OrderRequestBuilder<MarketOrderRequest> builder = tradeClient.CreateOrderRequestBuilder<MarketOrderRequest>();
        _ = builder
            .SetSymbolPair(symbolPair)
            .SetSide(OrderSide.Buy)
            .SetSizeInBaseSymbol(sizeInBaseSymbol: false)
            .SetSize(quoteSize);

        int orderCounter = 0;

        DateTime nextOrder = DateTime.MinValue;
        DateTime nextReport = DateTime.UtcNow.Add(reportPeriod);

        string reportFilePath = Path.Combine(appDataFolder, ReportFileName);

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

                nextOrder = time.Add(period);
                await PrintInfoAsync($"Next order should be placed at {nextOrder:yyyy-MM-dd HH:mm:ss} UTC.").ConfigureAwait(false);
            }

            time = DateTime.UtcNow;
            if (time >= nextReport)
            {
                await PrintInfoAsync($"Generating budget report ...").ConfigureAwait(false);
                await GenerateReportAsync(reportFilePath, tradeClient, cancellationToken).ConfigureAwait(false);

                nextReport = time.Add(reportPeriod);
                await PrintInfoAsync($"Next budgetReport should be generated at {nextReport:yyyy-MM-dd HH:mm:ss} UTC.").ConfigureAwait(false);
            }

            time = DateTime.UtcNow;
            TimeSpan delayTillOrder = nextOrder - time;
            TimeSpan delayTillReport = nextReport - time;
            TimeSpan delay = delayTillOrder < delayTillReport ? delayTillOrder : delayTillReport;

            if (delay > TimeSpan.Zero)
            {
                if (delay == delayTillOrder) await PrintInfoAsync($"Waiting {delay} before placing the next order.").ConfigureAwait(false);
                else await PrintInfoAsync($"Waiting {delay} before generating the next budget report.").ConfigureAwait(false);

                try
                {
                    await Task.Delay(period, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await PrintInfoAsync().ConfigureAwait(false);
                    await PrintInfoAsync("Shutdown detected.").ConfigureAwait(false);
                    break;
                }
            }
        }

        clog.Debug("$");
    }

    /// <summary>
    /// Places the order request to the exchange and waits for the order to be filled.
    /// </summary>
    /// <param name="tradeClient">Connected client.</param>
    /// <param name="orderRequest">Request for the order to place.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PlaceOrderAsync(ITradeApiClient tradeClient, MarketOrderRequest orderRequest, CancellationToken cancellationToken)
    {
        clog.Debug($" {nameof(tradeClient)}='{tradeClient}',{nameof(orderRequest)}='{orderRequest}'");

        ILiveMarketOrder order = await tradeClient.CreateOrderAsync(orderRequest, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FillData> fillData = await order.WaitForFillAsync(cancellationToken).ConfigureAwait(false);

        await PrintInfoAsync($"Order client ID '{order.ClientOrderId}' has been filled with {fillData.Count} trade(s).").ConfigureAwait(false);

        clog.Debug("$");
    }

    /// <summary>
    /// Generates report and writes its to the file, to the console, and to the log.
    /// </summary>
    /// <param name="reportFilePath">Full path to the report file.</param>
    /// <param name="tradeClient">Connected client.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task GenerateReportAsync(string reportFilePath, ITradeApiClient tradeClient, CancellationToken cancellationToken)
    {
        clog.Debug($" {nameof(reportFilePath)}='{reportFilePath}',{nameof(tradeClient)}='{tradeClient}'");

        BudgetReport budgetReport = await tradeClient.GenerateBudgetReportAsync(cancellationToken).ConfigureAwait(false);

        string initialValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.InitialValue}");
        string finalValueStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.FinalValue}");
        string totalProfitStr = string.Create(CultureInfo.InvariantCulture, $"{budgetReport.TotalProfit}");

        string reportLog = $$"""
            Budget report:
              start time: {{budgetReport.StartTime}} UTC
              end time: {{budgetReport.EndTime}} UTC
              initial value: {{initialValueStr}} {{budgetReport.PrimaryAsset}}
              final value: {{finalValueStr}} {{budgetReport.PrimaryAsset}}
              profit/loss: {{totalProfitStr}} {{budgetReport.PrimaryAsset}}
            """;

        await PrintInfoAsync(reportLog).ConfigureAwait(false);

        StringBuilder stringBuilder = new("Current budget:");
        _ = stringBuilder.AppendLine();

        foreach ((string assetName, decimal amount) in budgetReport.FinalBudget)
            _ = stringBuilder.AppendLine(CultureInfo.InvariantCulture, $" {assetName}: {amount}");

        _ = stringBuilder.AppendLine();

        string currentBudgetLog = stringBuilder.ToString();

        await PrintInfoAsync(currentBudgetLog).ConfigureAwait(false);

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
            _ = fileContentBuilder.Append(assetNames[i]);

            if (i != assetNames.Length - 1)
                _ = fileContentBuilder.Append(ReportFileValueSeparator);
        }

        _ = fileContentBuilder.AppendLine();

        decimal prevValue = 0;

        for (int i = -1; i < budgetReports.Count; i++)
        {
            BudgetSnapshot snapshot;

            if (i == -1)
            {
                // Second row is the initial budget line
                _ = fileContentBuilder
                    .Append(budgetReport.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(ReportFileValueSeparator)
                    .Append(ReportFileValueSeparator)
                    .Append(CultureInfo.InvariantCulture, $"{budgetReport.InitialValue}")
                    .Append(ReportFileValueSeparator)
                    .Append("0")
                    .Append(ReportFileValueSeparator)
                    .Append("0")
                    .Append(ReportFileValueSeparator);

                snapshot = budgetReport.InitialBudget;

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

                prevValue = report.FinalValue;
            }

            for (int assetNameIndex = 0; assetNameIndex < assetNames.Length; assetNameIndex++)
            {
                string assetName = assetNames[assetNameIndex];

                if (snapshot.TryGetValue(assetName, out decimal value))
                    _ = fileContentBuilder.Append(CultureInfo.InvariantCulture, $"{value}");

                _ = fileContentBuilder.Append(ReportFileValueSeparator);
            }

            _ = fileContentBuilder.AppendLine();
        }

        string fileContents = fileContentBuilder.ToString();

        // No cancellation token here to avoid losing data in case user presses Ctrl+C at the time of writing.
        await File.WriteAllTextAsync(reportFilePath, fileContents, CancellationToken.None).ConfigureAwait(false);

        clog.Debug("$");
    }

    /// <inheritdoc cref="ConnectionOptions.OnConnectedDelegateAsync"/>
    private static async Task OnConnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        await PrintInfoAsync().ConfigureAwait(false);
        await PrintInfoAsync("Connection to the exchange has been re-established successfully.").ConfigureAwait(false);
        await PrintInfoAsync().ConfigureAwait(false);
    }

    /// <inheritdoc cref="ConnectionOptions.OnDisconnectedDelegateAsync"/>
    private static async Task OnDisconnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        await PrintInfoAsync().ConfigureAwait(false);
        await PrintInfoAsync("CONNETION TO THE EXCHANGE HAS BEEN INTERRUPTED!!").ConfigureAwait(false);
        await PrintInfoAsync().ConfigureAwait(false);
    }
}