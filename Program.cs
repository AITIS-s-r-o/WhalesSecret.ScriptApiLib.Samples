using System.Diagnostics;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLibConsoleApp;

public class Program
{
    /// <summary>
    /// Application that fetches new ticker data and displays it.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task Main(string[] args)
    {
        await using ScriptApi api = await ScriptApi.CreateAsync(appDataFolder: "data", connectToBinanceSandbox: false, connectToKucoinSandbox: false);
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient client = await api.TradeApiV1.ConnectAsync(ExchangeMarket.BinanceSpot, connectionOptions).ConfigureAwait(false);
        await using ITickerSubscription tickerSubscription = await client.CreateTickerSubscriptionAsync(SymbolPair.BTC_USDT, CancellationToken.None).ConfigureAwait(false);

        while (true)
        {
            Ticker ticker = await tickerSubscription.GetNewerTickerAsync().ConfigureAwait(false);
            Console.WriteLine($"Ticker is: {ticker}");
            Debug.WriteLine($"Ticker is: {ticker}");
        }
    }
}