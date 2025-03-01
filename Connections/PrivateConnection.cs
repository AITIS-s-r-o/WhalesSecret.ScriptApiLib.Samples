using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.Connections;

/// <summary>
/// Sample that demonstrates how to connect to an exchange market using a private connection. Private connections are necessary when accessing information related to the user's
/// exchange account, or to create orders. These operations require exchange API credentials to be set.
/// </summary>
/// <remarks>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class PrivateConnection : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(timeoutCts.Token).ConfigureAwait(false);

        // Initialization of the market is required before connection can be created.
        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        _ = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

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

        await Console.Out.WriteLineAsync($"Connect to {exchangeMarket} exchange with a private connection.").ConfigureAwait(false);

        // Trading connection type means that only a private connection is established. Full-trading would create two connections, public and private.
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.Trading);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        await Console.Out.WriteLineAsync($"Private connection to {exchangeMarket} has been established successfully.").ConfigureAwait(false);

        // As the connection is established, we can use the connected client to, for example, query the time of the exchange.
        DateTime utcExchangeTime = tradeClient.GetExchangeUtcDateTime();
        TimeSpan diff = utcExchangeTime - DateTime.UtcNow;

        await Console.Out.WriteLineAsync($"Current UTC time of the {exchangeMarket} exchange is {utcExchangeTime}. The difference between the exchange time and the local time is {
            diff}.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync("Disposing trade API client and script API.").ConfigureAwait(false);
    }
}