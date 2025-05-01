using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.AccessLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Connections;

/// <summary>
/// Sample that demonstrates how to connect to an exchange market using a private connection. Private connections are necessary when accessing information related to the user's
/// exchange account, or to create orders. These operations require exchange API credentials to be set.
/// </summary>
/// <remarks>IMPORTANT: You have to change the keys and the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class PrivateConnection : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(timeoutCts.Token).ConfigureAwait(false);

        // Credentials must be set before we can create a private connection.

#pragma warning disable CA2000 // Dispose objects before losing scope
        IApiIdentity apiIdentity = exchangeMarket switch
        {
            ExchangeMarket.BinanceSpot => Credentials.GetBinanceHmacApiIdentity(),
            ExchangeMarket.KucoinSpot => Credentials.GetKucoinApiIdentity(),
            _ => throw new SanityCheckException($"Unsupported exchange market {exchangeMarket} provided."),
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        scriptApi.SetCredentials(apiIdentity);

        Console.WriteLine($"Connect to {exchangeMarket} exchange with a private connection.");

        // Trading connection type means that only a private connection is established. Full-trading would create two connections, public and private.
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.Trading);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        Console.WriteLine($"Private connection to {exchangeMarket} has been established successfully.");

        // As the connection is established, we can use the connected client to, for example, query the time of the exchange.
        DateTime utcExchangeTime = tradeClient.GetExchangeUtcDateTime();
        TimeSpan diff = utcExchangeTime - DateTime.UtcNow;

        Console.WriteLine($"Current UTC time of the {exchangeMarket} exchange is {utcExchangeTime}. The difference between the exchange time and the local time is {
            diff}.");

        Console.WriteLine("Disposing trade API client and script API.");
    }
}