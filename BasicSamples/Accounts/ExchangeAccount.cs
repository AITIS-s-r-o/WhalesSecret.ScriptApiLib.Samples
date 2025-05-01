using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.SharedLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.Account;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Accounts;

/// <summary>
/// Sample that demonstrates how get <see cref="ExchangeAccountInformation">exchange account information</see> for the user's exchange account. This structure contains information
/// about fees as well as information about balances in the exchange wallets.
/// </summary>
/// <remarks>
/// Note that in this sample, we only use <see cref="ITradeApiClient.GetLatestExchangeAccountInformation(string?)"/> to get the current state of exchange account. We do not use
/// <see cref="ITradeApiClient.GetNewerExchangeAccountInformationAsync(string?, CancellationToken)"/> that would allow us to wait for a change in that state (commonly change in
/// the wallet balance) as these are unlikely to happen without performing other actions.
/// <para>IMPORTANT: You have to change the secrets in <see cref="Credentials"/> to make the sample work.</para>
/// </remarks>
public class ExchangeAccount : IScriptApiSample
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

        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.Trading);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        Console.WriteLine($"Private connection to {exchangeMarket} has been established successfully.");

        // As the connection is established, we can consume the exchange account information. Without specification of a sub-account, the primary sub-account is used.
        ExchangeAccountInformation info = tradeClient.GetLatestExchangeAccountInformation();

        // The minimal value of the timestamp is reserved for the snapshots. In this sample, we are unlikely to get anything else than a snapshot since we do not await additional
        // updates. However, if updates were awaited, the timestamp would become meaningful.
        if (info.Timestamp != DateTime.MinValue)
        {
            Console.WriteLine($"The following information is valid at UTC time {info.Timestamp}.");
            Console.WriteLine();
        }

        if (info.MakerFee is not null)
            Console.WriteLine($"Your basic maker fee is {info.MakerFee * 100m} %.");

        if (info.TakerFee is not null)
            Console.WriteLine($"Your basic taker fee is {info.TakerFee * 100m} %.");

        Console.WriteLine();

        Console.WriteLine($"List of wallet balances on the primary sub-account:");
        foreach ((string symbolName, AccountSymbolInformation accountSymbolInformation) in info.SymbolsInformation)
        {
            Console.WriteLine($"  {symbolName}: {accountSymbolInformation.AvailableBalance} / {accountSymbolInformation.TotalBalance} is available.");
        }

        Console.WriteLine();
        Console.WriteLine("Disposing trade API client and script API.");
    }
}