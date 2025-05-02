using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to place (small) orders without a valid license.
/// </summary>
/// <seealso cref="SizeSampleCore"/>
/// <remarks>IMPORTANT: You have to change the keys and the secrets in <see cref="Credentials"/> to make the sample work.</remarks>
public class SizeSmall : IScriptApiSample
{
    /// <inheritdoc/>
    public Task RunSampleAsync(ExchangeMarket exchangeMarket)
        => SizeSampleCore.RunSampleAsync(exchangeMarket, useLargeOrder: false);
}