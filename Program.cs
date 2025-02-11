using System;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.Subscriptions;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples;

public class Program
{
    /// <summary>List of supported samples. Each sample is defined by a triplet - name of the sample, type of the sample class, and description.</summary>
    private static readonly object[][] sampleDescriptions = new object[][]
    {
        new object[] { "Subscriptions/Candle.Basic", typeof(CandleBasic), "Basic candle subscription sample. Demonstrates how candle subscriptions can created and consumed."},
        new object[] { "Subscriptions/OrderBook.Basic", typeof(OrderBookBasic), "Basic order book subscription sample. Demonstrates how an order book subscription can be created and "
            + "consumed."},
        new object[] { "Subscriptions/Ticker.Basic", typeof(TickerBasic), "Basic ticker subscription sample. Demonstrates how ticker subscription can be created and consumed."},
    };

    /// <summary>
    /// Application that fetches new ticker data and displays it.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task Main(string[] args)
    {
        if (args.Length == 2)
        {
            string? error = null;
            string sampleName = args[0];
            string exchangeMarketStr = args[1];

            foreach (object[] sample in sampleDescriptions)
            {
                string name = (string)sample[0];
                Type type = (Type)sample[1];
                if (sampleName.Equals(name, StringComparison.Ordinal))
                {
                    IScriptApiSample? instance = (IScriptApiSample?)Activator.CreateInstance(type);
                    if (instance is not null)
                    {
                        if (Enum.TryParse(exchangeMarketStr, out ExchangeMarket exchangeMarket))
                        {
                            await instance.RunSampleAsync(exchangeMarket).ConfigureAwait(false);
                            return;
                        }

                        error = $"'{exchangeMarketStr}' is not a valid exchange market.";
                    }
                    else error = $"Unable to create instance of '{type.FullName}'.";

                    break;
                }
            }

            await Console.Out.WriteLineAsync($$"""
                ERROR: {{error}}

            """).ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync($$"""
            Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(Samples)}} <sampleName> <exchangeMarket>

                sampleName - name of the sample to run, following values are supported:

            """).ConfigureAwait(false);

        foreach (object[] sample in sampleDescriptions)
        {
            string name = (string)sample[0];
            string description = (string)sample[2];
            await Console.Out.WriteLineAsync($"        {name} - {description}").ConfigureAwait(false);
        }

        string markets = string.Join(',', Enum.GetValues<ExchangeMarket>());
        await Console.Out.WriteLineAsync($$"""

                exchangeMarket - which exchange market should the sample connect to; supported values are {{markets}}
            """).ConfigureAwait(false);
    }
}