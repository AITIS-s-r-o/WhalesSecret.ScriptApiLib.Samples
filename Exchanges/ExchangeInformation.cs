using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exchanges;

namespace WhalesSecret.ScriptApiLib.Samples.Exchanges;

/// <summary>
/// Sample that demonstrates what kind of information we can get from the exchange market initialization.
/// <para>
/// This sample demonstrates manual initialization of the exchange market without creating connection to the market. If connection is to be established, the better way of getting
/// exchange information is from the connected client using <see cref="ITradeApiClient.GetExchangeInfo"/>.
/// </para>
/// </summary>
public class ExchangeInformation : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(timeoutCts.Token).ConfigureAwait(false);

        // Initialization of the market is required before connection can be created.
        Console.WriteLine($"Initialize exchange market {exchangeMarket}.");
        ExchangeInfo exchangeInfo = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Difference between the exchange time and the local time of the {exchangeMarket} exchange is {exchangeInfo.TimeShift}.");

        Console.WriteLine();
        Console.WriteLine($"The following symbol pairs are supported by {exchangeMarket}:");

        foreach ((SymbolPair symbolPair, ExchangeSymbolPairLimits limits) in exchangeInfo.SymbolPairLimits)
            Console.WriteLine($"  {symbolPair}: {limits}");

        Console.WriteLine();
        Console.WriteLine("Disposing script API.");
    }
}