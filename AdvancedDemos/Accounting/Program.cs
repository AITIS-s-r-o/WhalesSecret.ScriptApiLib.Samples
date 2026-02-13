using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.API.TradingV1.Orders;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.ExchangeAccounts;
using WhalesSecret.TradeScriptLib.Entities.Orders;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.Accounting;

/// <summary>
/// Accounting demo. This program creates a CSV summary report of trading, deposits, withdrawals, and fees for the specified period on the given exchange market.
/// <para>The summary provides end-of-month values for each month within the covered period for each of the asset that was traded, deposited, or withdrawn on the exchange.</para>
/// </summary>
internal class Program
{
    /// <summary>Format of the name of the summary report file. The format has three parameters - exchange name, start date, and end date.</summary>
    private const string SummaryReportFileNameFormat = "{0}_summary_{1:yyyy-MM}_{2:yyyy-MM}.csv";

    /// <summary>Format of the name of the trades report file. The format has three parameters - exchange name, start date, and end date.</summary>
    private const string TradesReportFileNameFormat = "{0}_trades_{1:yyyy-MM}_{2:yyyy-MM}.csv";

    /// <summary>Format of the name of the deposits/withdrawals report file. The format has three parameters - exchange name, start date, and end date.</summary>
    private const string DepositsWithdrawalsReportFileNameFormat = "{0}_deposits_withdrawals_{1:yyyy-MM}_{2:yyyy-MM}.csv";

    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

    /// <summary>
    /// Application that creates accounting summary for an exchange account.
    /// </summary>
    /// <param name="args">Command-line arguments.
    /// <para>The program must be started with 1 argument - the input parameters JSON file path.</para>
    /// <para>Example input file:
    /// <code>
    /// {
    ///   "System": {
    ///     "License": "INSERT YOUR WHALES SECRET LICENSE HERE OR USE null TO USE FREE MODE",
    ///     "AppDataPath": "Data",
    ///     "ApiKeys": {
    ///       "Binance": {
    ///         "HmacKey": "INSERT YOUR Binance HMAC API key HERE OR USE null TO USE RSA key",
    ///         "HmacSecret": "INSERT YOUR Binance HMAC API key HERE OR USE null TO USE RSA key",
    ///         "RsaKey": "INSERT YOUR Binance RSA API key HERE OR USE null TO USE HMAC key",
    ///         "RsaSecret": "INSERT YOUR Binance RSA API secret OR USE null TO USE HMAC key"
    ///       },
    ///       "Kucoin": {
    ///         "Key": "INSERT YOUR Kucoin API key HERE",
    ///         "Secret": "INSERT YOUR Kucoin API secret HERE",
    ///         "Passphrase": "INSERT YOUR Kucoin API passphrase HERE"
    ///       }
    ///     }
    ///   },
    ///   "ExchangeMarket": "BinanceSpot",
    ///   "StartDate": "2025-01-01",
    ///   "EndDate": "2025-12-31"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// With this input the program will make an accounting summary from Binance Spot market for the whole year <c>2025</c>.
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
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(Samples)}}.{{nameof(AdvancedDemos)}}.{{nameof(Accounting)}} <parametersFilePath>
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

        PrintInfo($"Starting accounting summary on {parameters.ExchangeMarket} for period {parameters.StartDate} to {parameters.EndDate}.");
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
            await StartAccountingAsync(parameters, shutdownToken).ConfigureAwait(false);
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
    /// Starts the accounting task.
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
    private static async Task StartAccountingAsync(Parameters parameters, CancellationToken cancellationToken)
    {
        clog.Debug($"* {nameof(parameters)}='{parameters}'");

        // In order to unlock large orders, a valid license has to be used.
        CreateOptions createOptions = new(appDataFolder: parameters.System.AppDataPath, license: parameters.System.License);
        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(createOptions, cancellationToken).ConfigureAwait(false);

#pragma warning disable CA2000 // Dispose objects before losing scope
        IApiIdentity apiIdentity = parameters.System.ApiKeys.GetApiIdentity(parameters.ExchangeMarket);
#pragma warning restore CA2000 // Dispose objects before losing scope

        scriptApi.SetCredentials(apiIdentity);

        ConnectionOptions connectionOptions = new(BlockUntilReconnectedOrTimeout.InfinityTimeoutInstance, ConnectionType.FullTrading);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(parameters.ExchangeMarket, connectionOptions).ConfigureAwait(false);

        PrintInfo($"Connection to {parameters.ExchangeMarket} has been established successfully.");

        DateOnly startDate = parameters.StartDate;
        DateOnly endDate = parameters.EndDate;
        ExchangeMarket exchangeMarket = parameters.ExchangeMarket;

        SortedDictionary<DateOnly, Dictionary<string, AssetStats>> monthlyStats = new();

        StringBuilder tradesSb = new("Time,Base Symbol,Quote Symbol,Side,Price,Base Amount,Quote Amount,Fee,Fee Symbol");
        _ = tradesSb.AppendLine();

        StringBuilder depositsWithdrawalsSb = new("Time,Type,ID,Amount,Symbol,Network,Fee,Status,Address,TxId,Extra Info");
        _ = depositsWithdrawalsSb.AppendLine();

        Dictionary<string, AssetStats>? currentMonthStats = null;
        DateOnly date = startDate;
        while (date <= endDate)
        {
            if ((date.Day == 1) || (currentMonthStats is null))
            {
                Dictionary<string, AssetStats>? prevMonthStats = currentMonthStats;

                currentMonthStats = new();
                monthlyStats[date] = currentMonthStats;

                if (prevMonthStats is not null)
                {
                    foreach ((string asset, AssetStats stats) in prevMonthStats)
                    {
                        currentMonthStats[asset] = new AssetStats
                        {
                            TotalDiff = stats.TotalDiff,
                        };
                    }
                }
            }

            PrintInfo($"Downloading trades for {date:yyyy-MM-dd}.");

            IReadOnlyList<ITrade> trades = Array.Empty<ITrade>();
            try
            {
                trades = await tradeClient.GetTradesAsync(date, cancellationToken).ConfigureAwait(false);
                PrintInfo($"Found {trades.Count} trades.");
            }
            catch (InvalidRequestDataException)
            {
                PrintInfo($"Date {date} is before the account's first deposit.");
            }
            catch (InvalidArgumentException)
            {
                PrintInfo($"Date {date} is in the future.");
                break;
            }

            foreach (ITrade trade in trades)
            {
                AppendTradeLine(tradesSb, trade);

                if (!currentMonthStats.TryGetValue(trade.SymbolPair.BaseSymbol, out AssetStats? baseStats))
                {
                    baseStats = new AssetStats();
                    currentMonthStats[trade.SymbolPair.BaseSymbol] = baseStats;
                }

                baseStats.TotalDiff += trade.Side == OrderSide.Buy ? trade.BaseQuantity : -trade.BaseQuantity;
                baseStats.TradingDiff += trade.Side == OrderSide.Buy ? trade.BaseQuantity : -trade.BaseQuantity;

                if (!currentMonthStats.TryGetValue(trade.SymbolPair.QuoteSymbol, out AssetStats? quoteStats))
                {
                    quoteStats = new AssetStats();
                    currentMonthStats[trade.SymbolPair.QuoteSymbol] = quoteStats;
                }

                quoteStats.TotalDiff += trade.Side == OrderSide.Buy ? -trade.QuoteQuantity : trade.QuoteQuantity;
                quoteStats.TradingDiff += trade.Side == OrderSide.Buy ? -trade.QuoteQuantity : trade.QuoteQuantity;

                if ((trade.CommissionAsset is not null) && (trade.CommissionAmount is not null))
                {
                    if (!currentMonthStats.TryGetValue(trade.CommissionAsset, out AssetStats? feeStats))
                    {
                        feeStats = new AssetStats();
                        currentMonthStats[trade.CommissionAsset] = feeStats;
                    }

                    feeStats.TotalDiff -= trade.CommissionAmount.Value;
                    feeStats.FeeDiff -= trade.CommissionAmount.Value;
                }
            }

            PrintInfo($"Downloading deposits for {date:yyyy-MM-dd}.");
            IReadOnlyList<DepositInformation> deposits = await tradeClient.GetDepositsAsync(startDate: date, endDate: date, cancellationToken).ConfigureAwait(false);
            PrintInfo($"Found {deposits.Count} deposits.");

            foreach (DepositInformation deposit in deposits)
            {
                AppendDepositLine(depositsWithdrawalsSb, deposit);

                if (!currentMonthStats.TryGetValue(deposit.Asset, out AssetStats? depositStats))
                {
                    depositStats = new AssetStats();
                    currentMonthStats[deposit.Asset] = depositStats;
                }

                decimal fee = deposit.Fee;

                // In case of deposits, the deposit amount is after the fees. So the fee has to be added to the deposit as we will also have the fee separately in the table.
                depositStats.TotalDiff += deposit.Amount;
                depositStats.DepositDiff += deposit.Amount + fee;

                depositStats.FeeDiff -= fee;
            }

            PrintInfo($"Downloading withdrawals for {date:yyyy-MM-dd}.");
            IReadOnlyList<WithdrawalInformation> withdrawals = await tradeClient.GetWithdrawalsAsync(startDate: date, endDate: date, cancellationToken).ConfigureAwait(false);
            PrintInfo($"Found {withdrawals.Count} withdrawals.");

            foreach (WithdrawalInformation withdrawal in withdrawals)
            {
                AppendWithdrawalLine(depositsWithdrawalsSb, withdrawal);

                if (!currentMonthStats.TryGetValue(withdrawal.Asset, out AssetStats? withdrawalStats))
                {
                    withdrawalStats = new AssetStats();
                    currentMonthStats[withdrawal.Asset] = withdrawalStats;
                }

                withdrawalStats.TotalDiff -= withdrawal.Amount;
                withdrawalStats.WithdrawalDiff -= withdrawal.Amount;

                withdrawalStats.FeeDiff -= withdrawal.Fee;
            }

            date = date.AddDays(1);
        }

        string path = string.Format(CultureInfo.InvariantCulture, SummaryReportFileNameFormat, exchangeMarket, startDate, endDate);

        PrintInfo();
        PrintInfo($"Generating the summary CSV report to '{path}'.");

        GenerateSummaryCsv(monthlyStats, path);

        path = string.Format(CultureInfo.InvariantCulture, TradesReportFileNameFormat, exchangeMarket, startDate, endDate);

        PrintInfo();
        PrintInfo($"Generating the trades CSV report to '{path}'.");

        File.WriteAllText(path, tradesSb.ToString());

        path = string.Format(CultureInfo.InvariantCulture, DepositsWithdrawalsReportFileNameFormat, exchangeMarket, startDate, endDate);

        PrintInfo();
        PrintInfo($"Generating the deposits/withdrawals CSV report to '{path}'.");

        File.WriteAllText(path, depositsWithdrawalsSb.ToString());

        PrintInfo();
        PrintInfo("All done!");
    }

    /// <summary>
    /// Adds one line to trades report with information about the given trade.
    /// </summary>
    /// <param name="sb">String builder for trades report.</param>
    /// <param name="trade">Trade to add.</param>
    private static void AppendTradeLine(StringBuilder sb, ITrade trade)
    {
        _ = sb
            .Append(CultureInfo.InvariantCulture, $"{trade.Timestamp:yyyy-MM-dd HH:mm:ss}")
            .Append(',')
            .Append(trade.SymbolPair.BaseSymbol)
            .Append(',')
            .Append(trade.SymbolPair.QuoteSymbol)
            .Append(',')
            .Append(trade.Side == OrderSide.Buy ? "BUY" : "SELL")
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{trade.Price}")
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{trade.BaseQuantity}")
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{trade.QuoteQuantity}")
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{trade.CommissionAmount}")
            .Append(',')
            .AppendLine(trade.CommissionAsset);
    }

    /// <summary>
    /// Adds one line to deposits/withdrawals report with information about the given deposit.
    /// </summary>
    /// <param name="sb">String builder for deposits/withdrawals report.</param>
    /// <param name="deposit">Deposit to add.</param>
    private static void AppendDepositLine(StringBuilder sb, DepositInformation deposit)
    {
        _ = sb
            .Append(CultureInfo.InvariantCulture, $"{deposit.RecordTime:yyyy-MM-dd HH:mm:ss}")
            .Append(",DEPOSIT,")
            .Append(deposit.ExchangeDepositId)
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{deposit.Amount}")
            .Append(',')
            .Append(deposit.Asset)
            .Append(',')
            .Append(deposit.Network)
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{deposit.Fee}")
            .Append(',')
            .Append(deposit.Status)
            .Append(',')
            .Append(deposit.Address)
            .Append(',')
            .Append(deposit.TxId)
            .Append(',')
            .AppendLine(deposit.ExtraInfo);
    }

    /// <summary>
    /// Adds one line to deposits/withdrawals report with information about the given withdrawal.
    /// </summary>
    /// <param name="sb">String builder for deposits/withdrawals report.</param>
    /// <param name="withdrawal">Withdrawal to add.</param>
    private static void AppendWithdrawalLine(StringBuilder sb, WithdrawalInformation withdrawal)
    {
        _ = sb
            .Append(CultureInfo.InvariantCulture, $"{withdrawal.RecordTime:yyyy-MM-dd HH:mm:ss}")
            .Append(",WITHDRAWAL,")
            .Append(withdrawal.ExchangeWithdrawalId)
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{withdrawal.Amount}")
            .Append(',')
            .Append(withdrawal.Asset)
            .Append(',')
            .Append(withdrawal.Network)
            .Append(',')
            .Append(CultureInfo.InvariantCulture, $"{withdrawal.Fee}")
            .Append(',')
            .Append(withdrawal.Status)
            .Append(',')
            .Append(withdrawal.Address)
            .Append(',')
            .Append(withdrawal.TxId)
            .Append(',')
            .AppendLine(withdrawal.ExtraInfo);
    }

    /// <summary>
    /// Generates the summary CSV report from the collected end-of-month stats.
    /// </summary>
    /// <param name="monthlyStats">Per-asset stats for end of each month.</param>
    /// <param name="outputPath">Path to the output file.</param>
    public static void GenerateSummaryCsv(SortedDictionary<DateOnly, Dictionary<string, AssetStats>> monthlyStats, string outputPath)
    {
        List<string> allAssets = monthlyStats
            .SelectMany(kv => kv.Value.Keys)
            .Distinct()
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<DateOnly> months = monthlyStats.Keys.ToList();

        StringBuilder sb = new();

        // Header row contains corner cell + month names + "Total" column.
        List<string> header = new() { "Month / Asset - Type" };

        // Month names (Jan 2025, Feb 2025, ...).
        foreach (DateOnly date in months)
        {
            string monthName = date.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            header.Add(monthName);
        }

        // Last column = Total.
        header.Add("Total");

        WriteLine(sb, header);

        // Content rows - one block per asset contains 6 rows (asset name, trading, deposits, withdrawals, fees, total balance).
        foreach (string asset in allAssets)
        {
            // Row 1: Asset name (only in first column).
            WriteLine(sb, new[] { asset }.Concat(Enumerable.Repeat(string.Empty, months.Count + 1)));

            // Prepare rows for the 5 diff types + total balance.
            List<List<string>> rows = new()
            {
                new() { "  Trading" },
                new() { "  Deposits" },
                new() { "  Withdrawals" },
                new() { "  Fees" },
                new() { "  Total balance" },
            };

            // Fill values for each month.
            AssetStats totals = new();
            foreach (DateOnly monthDate in months)
            {
                string trading = string.Empty;
                string deposit = string.Empty;
                string withdrawal = string.Empty;
                string fee = string.Empty;
                string total = string.Empty;

                if (monthlyStats[monthDate].TryGetValue(asset, out AssetStats? stats))
                {
                    trading = FormatDecimal(stats.TradingDiff);
                    deposit = FormatDecimal(stats.DepositDiff);
                    withdrawal = FormatDecimal(stats.WithdrawalDiff);
                    fee = FormatDecimal(stats.FeeDiff);
                    total = FormatDecimal(stats.TotalDiff);

                    totals.TradingDiff += stats.TradingDiff;
                    totals.DepositDiff += stats.DepositDiff;
                    totals.WithdrawalDiff += stats.WithdrawalDiff;
                    totals.FeeDiff += stats.FeeDiff;

                    // Total balance is taken from the last month that has data for this asset.
                    totals.TotalDiff = stats.TotalDiff;
                }

                rows[0].Add(trading);
                rows[1].Add(deposit);
                rows[2].Add(withdrawal);
                rows[3].Add(fee);
                rows[4].Add(total);
            }

            rows[0].Add(FormatDecimal(totals.TradingDiff));
            rows[1].Add(FormatDecimal(totals.DepositDiff));
            rows[2].Add(FormatDecimal(totals.WithdrawalDiff));
            rows[3].Add(FormatDecimal(totals.FeeDiff));
            rows[4].Add(FormatDecimal(totals.TotalDiff));

            // Write all 5 data rows
            foreach (List<string> row in rows)
            {
                WriteLine(sb, row);
            }

            // Empty line between assets for better readability.
            _ = sb.AppendLine();
        }

        string csvContent = sb.ToString();
        File.WriteAllText(outputPath, csvContent, Encoding.UTF8);
    }

    /// <summary>
    /// Writes a comma-separated list of values to the string builder as a new line.
    /// </summary>
    /// <param name="sb">String builder.</param>
    /// <param name="values">Values to write.</param>
    private static void WriteLine(StringBuilder sb, IEnumerable<string> values)
    {
        _ = sb.AppendLine(string.Join(",", values));
    }

    /// <summary>
    /// Converts a decimal number to a string, formatting it to up to <c>8</c> decimal places with no trailing zeros.
    /// </summary>
    /// <param name="value">Decimal value to format.</param>
    /// <returns>String representation of the decimal value.</returns>
    private static string FormatDecimal(decimal value)
        => value.ToString("0.########", CultureInfo.InvariantCulture).TrimEnd('.');
}