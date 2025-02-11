using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples;

/// <summary>
/// ScriptApiLib runnable sample.
/// </summary>
public interface IScriptApiSample
{
    /// <summary>
    /// Run the sample.
    /// </summary>
    /// <param name="exchangeMarket">Exchange market to connect to.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RunSampleAsync(ExchangeMarket exchangeMarket);
}