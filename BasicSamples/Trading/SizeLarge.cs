using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Trading;

/// <summary>
/// Sample that demonstrates how to place large orders.
/// </summary>
/// <seealso cref="SizeSampleCore"/>
/// <remarks>IMPORTANT: You have to change the keys and the secrets in <see cref="Credentials"/> and put a valid license to <see cref="License"/> to make the sample work.</remarks>
public class SizeLarge : IScriptApiSample
{
    /// <inheritdoc/>
    public Task RunSampleAsync(ExchangeMarket exchangeMarket)
        => SizeSampleCore.RunSampleAsync(exchangeMarket, useLargeOrder: true);
}