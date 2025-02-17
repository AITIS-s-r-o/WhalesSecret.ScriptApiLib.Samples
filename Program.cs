using System;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.Accounts;
using WhalesSecret.ScriptApiLib.Samples.Connections;
using WhalesSecret.ScriptApiLib.Samples.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.Subscriptions;
using WhalesSecret.ScriptApiLib.Samples.Trading;
using WhalesSecret.TradeScriptLib.Entities;

namespace WhalesSecret.ScriptApiLib.Samples;

/// <summary>
/// Main application class that contains program entry point.
/// </summary>
public class Program
{
    /// <summary>List of supported samples. Each sample is defined by a triplet - name of the sample, type of the sample class, and description.</summary>
    private static readonly object[][] sampleDescriptions = new object[][]
    {
        new object[] { "Accounts/ExchangeAccount", typeof(ExchangeAccount), "Demonstrates how to get balances for assets in the exchange account wallet and basic trading fees."},
        new object[] { "Connections/Public", typeof(PublicConnection), "Demonstrates how to connect to an exchange via public connection."},
        new object[] { "Connections/Private", typeof(PrivateConnection), "Demonstrates how to connect to an exchange via private connection using exchange API credentials."},
        new object[] { "Exchanges/ExchangeInformation", typeof(ExchangeInformation), "Demonstrates what kind of information can we get from the exchange market initialization."},
        new object[] { "Orders/Size.Small", typeof(SizeSmall), "Basic order sample. Demonstrates how small-sized orders can be placed."},
        new object[] { "Orders/Size.Large", typeof(SizeLarge), "Basic order sample. Demonstrates how larger-sized orders can be placed. Requires a valid license."},
        new object[] { "Subscriptions/Candle.Basic", typeof(CandleBasic), "Basic candle subscription sample. Demonstrates how a candle subscription can created and consumed."},
        new object[] { "Subscriptions/Candle.Set", typeof(CandleSet), "Advanced candle subscription sample. Demonstrates how multiple candlestick subscriptions can be created and"
            + " consumed at the same time."},
        new object[] { "Subscriptions/OrderBook.Basic", typeof(OrderBookBasic), "Basic order book subscription sample. Demonstrates how an order book subscription can be created"
            + " and consumed."},
        new object[] { "Subscriptions/Ticker.Basic", typeof(TickerBasic), "Basic ticker subscription sample. Demonstrates how a ticker subscription can be created and consumed."},
        new object[] { "Subscriptions/Ticker.Set", typeof(TickerSet), "Advanced ticker subscription sample. Demonstrates how multiple ticker subscriptions can be created and"
            + " consumed at the same time."},
    };

    /// <summary>
    /// Application that fetches new ticker data and displays it.
    /// </summary>
    /// <param name="args">Command-line arguments.
    /// <para>The program must be started with 2 arguments given in this order:
    /// <list type="table">
    /// <item><c>sampleName</c> – Name of the sample to run.</item>
    /// <item><c>exchangeMarket</c> – <see cref="ExchangeMarket">Exchange market</see> to use in the sample.</item>
    /// </list>
    /// </para>
    /// <para>Run the program without any arguments to see the supported values for each argument.</para>
    /// </param>
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

                sampleName - Name of the sample to run. Following values are supported:

            """).ConfigureAwait(false);

        foreach (object[] sample in sampleDescriptions)
        {
            string name = (string)sample[0];
            string description = (string)sample[2];
            await Console.Out.WriteLineAsync($"        {name} - {description}").ConfigureAwait(false);
        }

        string markets = string.Join(',', Enum.GetValues<ExchangeMarket>());
        await Console.Out.WriteLineAsync($$"""

                exchangeMarket - Exchange market to use in the sample. Supported values are {{markets}}
            """).ConfigureAwait(false);
    }
}