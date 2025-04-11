using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples.Connections;

/// <summary>
/// Sample that demonstrates how to connect to an exchange market using a public connection. Public connections are commonly used for accessing publicly available information,
/// such as candlesticks, order books, or tickers.
/// </summary>
public class PublicConnection : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Connect to {exchangeMarket} exchange with a public connection.");

        // Market-data connection type is the only connection type that does not need exchange API credentials.
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        Console.WriteLine($"Public connection to {exchangeMarket} has been established successfully.");

        // As the connection is established, we can use the connected client to, for example, query the time of the exchange.
        DateTime utcExchangeTime = tradeClient.GetExchangeUtcDateTime();
        TimeSpan diff = utcExchangeTime - DateTime.UtcNow;

        Console.WriteLine($"Current UTC time of the {exchangeMarket} exchange is {utcExchangeTime}. The difference between the exchange time and the local time is {
            diff}.");

        Console.WriteLine("Disposing trade API client and script API.");
    }
}