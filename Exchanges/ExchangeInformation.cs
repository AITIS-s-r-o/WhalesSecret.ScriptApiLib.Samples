using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Exchanges;

namespace WhalesSecret.ScriptApiLib.Samples.Exchanges;

/// <summary>
/// Sample that demonstrates what kind of information we can get from the exchange market initialization.
/// </summary>
public class ExchangeInformation : IScriptApiSample
{
    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync().ConfigureAwait(false);

        // Initialization of the market is required before connection can be created.
        await Console.Out.WriteLineAsync($"Initialize exchange market {exchangeMarket}.").ConfigureAwait(false);
        ExchangeInfo exchangeInfo = await scriptApi.InitializeMarketAsync(exchangeMarket, timeoutCts.Token).ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Difference between the exchange time and the local time of the {exchangeMarket} exchange is {exchangeInfo.TimeShift}.")
            .ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"The following symbol pairs are supported by {exchangeMarket}:").ConfigureAwait(false);

        foreach ((SymbolPair symbolPair, ExchangeSymbolPairLimits limits) in exchangeInfo.SymbolPairLimits)
            await Console.Out.WriteLineAsync($"  {symbolPair}: {limits}").ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync("Disposing script API.").ConfigureAwait(false);
    }
}