using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.ConnectionStrategy;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.TradeScriptLib.Logging;

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
    ///   "Leverage": 2.0
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// With this input the program will calculate how would L-DCA perform if it was executed on Binance exchange over the whole year 2024, buying <c>10</c> EUR worth of BTC every
    /// day with <c>0.1</c>% trading fee and <c>2</c>x leverage.
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

        PrintInfo($"Downloading historical data for '{parameters.SymbolPair}' between {parameters.StartTimeUtc:yyyy-MM-dd HH:mm:ss} and {
            parameters.EndTimeUtc:yyyy-MM-dd HH:mm:ss}...");

        CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(parameters.SymbolPair, CandleWidth.Minute1, startTime: parameters.StartTimeUtc,
            endTime: parameters.EndTimeUtc, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}