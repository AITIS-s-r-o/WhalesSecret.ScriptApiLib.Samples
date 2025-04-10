using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.Budget;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

namespace WhalesSecret.ScriptApiLib.DCA;

/// <summary>
/// Main application class that contains program entry point.
/// </summary>
internal class Program
{
    private const string StrategyName = "DCA";

    /// <summary>Class logger.</summary>
    private static readonly WsLogger clog = WsLogger.GetCurrentClassLogger();

    /// <summary>
    /// Application that fetches new ticker data and displays it.
    /// </summary>
    /// <param name="args">Command-line arguments.
    /// <para>The program must be started with 5 arguments given in this order:
    /// <list type="table">
    /// <item><c>exchangeMarket</c> – <see cref="ExchangeMarket">Exchange market</see> to DCA at.</item>
    /// <item><c>symbolPair</c> – Symbol pair to DCA, e.g. <c>BTC/EUR</c>.</item>
    /// <item><c>periodSeconds</c> – Time period in seconds between the orders, e.g. <c>3600</c> for 1 hour period.</item>
    /// <item><c>quoteSize</c> – Order size in quote symbol, e.g. <c>10</c> to buy <c>10</c> EUR worth of BTC with each trade.</item>
    /// <item><c>budget</c> – Comma-separated list of budget allocations for each asset with the primary asset being first. Each budget allocation is a pair of asset name
    /// and the amount, separated by an equal sign. E.g. <c>EUR=100,BTC=0.1</c> would allocate budget with primary asset EUR that can has <c>100</c> EUR and <c>0.1</c> BTC
    /// available.</item>
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

        if (args.Length == 5)
        {
            string exchangeMarketStr = args[0];
            string symbolPairStr = args[1];
            string periodSecondsStr = args[2];
            string quoteSizeStr = args[3];
            string budgetStr = args[4];

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
            else 
            {
                exchangeMarket = exchangeMarketParsed;
                symbolPair = symbolPairParsed;
                periodSeconds = periodSecondsParsed;
                quoteSize = quoteSizeParsed;
                budgetRequest = budgetRequestParsed;
            }
        }

        if (error is not null)
        {
            await Console.Error.WriteLineAsync($$"""
                ERROR: {{error}}

                """).ConfigureAwait(false);
        }

        if ((exchangeMarket is null) || (symbolPair is null) || (periodSeconds is null) || (quoteSize is null) || (budgetRequest is null))
        {
            string markets = string.Join(',', Enum.GetValues<ExchangeMarket>());

            await Console.Out.WriteLineAsync($$"""
                Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(DCA)}} <exchangeMarket> <symbolPair> <periodSeconds> <quoteSize> <budget>

                    exchangeMarket - Exchange market to use in the sample. Supported values are {{markets}}.
                    symbolPair - Symbol pair to DCA, e.g. "BTC/EUR".
                    periodSeconds - Time period in seconds between the orders, e.g. "3600" for 1 hour period.
                    quoteSize - Order size in quote symbol, e.g. "10" to buy 10 EUR worth of BTC with each trade.
                    budget - Comma-separated list of budget allocations for each asset with the primary asset being first. Each budget allocation is a pair of asset name{{
                " "}}and the amount, separated by an equal sign. E.g. "EUR=100,BTC=0.1" would allocate budget with primary asset EUR that can has 100 EUR and 0.1 BTC{{
                " "}}available.
                """).ConfigureAwait(false);

            clog.Info($"$<USAGE>");
            clog.FlushAndShutDown();

            return;
        }

        await PrintInfoAsync($"Starting DCA on {exchangeMarket}, buying {quoteSize} {symbolPair.Value.QuoteSymbol} worth of {symbolPair.Value.BaseSymbol} every {
            periodSeconds} seconds.").ConfigureAwait(false);
        await PrintInfoAsync($"Budget request: {budgetRequest}").ConfigureAwait(false);

        using CancellationTokenSource shutdownCancellationTokenSource = new();
        CancellationToken shutdownToken = shutdownCancellationTokenSource.Token;

        // Install Ctrl+C / SIGINT handler.
        ConsoleCancelEventHandler ControlCancelHandler = (object? sender, ConsoleCancelEventArgs e) =>
        {
            clog.Info("[CCEH] *");

            // If cancellation of the control event is set to true, the process won't terminate automatically and we will have a control over the shutdown.
            e.Cancel = true;
            shutdownCancellationTokenSource.Cancel();

            clog.Info("[CCEH] $");
        };

        Console.CancelKeyPress += ControlCancelHandler;

        try
        {
            string appPath = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
            string appDataFolder = Path.Combine(appPath, "Data");
            await StartDcaAsync(appDataFolder, exchangeMarket.Value, symbolPair.Value, TimeSpan.FromSeconds(periodSeconds.Value), quoteSize.Value, budgetRequest, shutdownToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // Uninstall Ctrl+C / SIGINT handler.
            Console.CancelKeyPress -= ControlCancelHandler;
        }

        clog.Info($"$");
        clog.FlushAndShutDown();
    }


    /// <summary>
    /// Prints information level message to the console and to the log. Message timestamp is added when printing to the console.
    /// </summary>
    /// <param name="msg">Message to print.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PrintInfoAsync(string msg)
    {
        clog.Info(msg);

        DateTime dateTime = DateTime.UtcNow;
        string dateTimeStr = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        await Console.Out.WriteLineAsync($"{dateTimeStr}: {msg}").ConfigureAwait(false);
    }

    /// <summary>
    /// Parses input budget string from the command line.
    /// </summary>
    /// <param name="str">String to parse.</param>
    /// <param name="budgetRequest">If the function succeeds, this is filled with the parsed configuration.</param>
    /// <returns><c>true</c> if parsing was successful, <c>false</c> if the format of the string to parse was invalid.</returns>
    private static bool TryParseBudget(string budgetStr, [NotNullWhen(true)] out BudgetRequest? budgetRequest)
    {
        clog.Debug($"* {nameof(budgetStr)}='{budgetStr}'");

        bool result = false;
        budgetRequest = null;

        string[] assetAllocations = budgetStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
    /// <param name="period">Time period in seconds between the orders.</param>
    /// <param name="quoteSize">Order size in quote symbol.</param>
    /// <param name="budgetRequest">Description of budget parameters for the trading strategy.</param>
    /// <param name="cancellationToken">Cancellation token that allows the caller to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task StartDcaAsync(string appDataFolder, ExchangeMarket exchangeMarket, SymbolPair symbolPair, TimeSpan period, decimal quoteSize,
        BudgetRequest budgetRequest, CancellationToken cancellationToken)
    {
        clog.Info($"* {nameof(appDataFolder)}='{appDataFolder}',{nameof(exchangeMarket)}={exchangeMarket},{nameof(symbolPair)}='{symbolPair}',{nameof(period)}={period},{
            nameof(quoteSize)}={quoteSize},{nameof(budgetRequest)}='{budgetRequest}'");

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

        string msg = $"Connect to {exchangeMarket} exchange with full-trading access.";
        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        clog.Info(msg);

        ConnectionOptions connectionOptions = new(ConnectionType.FullTrading, OnConnectedAsync, OnDisconnectedAsync, budgetRequest: budgetRequest);
        ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        clog.Info("$");
    }

    /// <inheritdoc cref="ConnectionOptions.OnConnectedDelegateAsync"/>
    private static Task OnConnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        clog.Info("*$");
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="ConnectionOptions.OnDisconnectedDelegateAsync"/>
    private static Task OnDisconnectedAsync(ITradeApiClient tradeApiClient)
    {
        // Just log the event.
        clog.Info("*$");
        return Task.CompletedTask;
    }
}