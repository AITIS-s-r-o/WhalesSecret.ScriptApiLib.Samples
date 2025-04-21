using System;
using System.Threading.Tasks;
using WhalesSecret.ScriptApiLib.Samples.Accounts;
using WhalesSecret.ScriptApiLib.Samples.Connections;
using WhalesSecret.ScriptApiLib.Samples.Exchanges;
using WhalesSecret.ScriptApiLib.Samples.Indicators;
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
        new object[] { "Accounts/ExchangeAccount", typeof(ExchangeAccount), "Demonstrates how to get balances for assets in the exchange account wallet and basic trading fees."
            + " Requires credentials."},
        new object[] { "Connections/Public", typeof(PublicConnection), "Demonstrates how to connect to an exchange via public connection."},
        new object[] { "Connections/Private", typeof(PrivateConnection), "Demonstrates how to connect to an exchange via private connection using exchange API credentials."
            + " Requires credentials."},
        new object[] { "Exchanges/ExchangeInformation", typeof(ExchangeInformation), "Demonstrates what kind of information can we get from the exchange market initialization."},
        new object[] { "Indicators/RSI", typeof(Rsi), "Demonstrates how to integrate an RSI indicator from a third party with Whale's Secrets ScriptApiLib. The sample also"
            + " demonstrates how to retrieve historical candle data."},
        new object[] { "Subscriptions/Candle.Basic", typeof(CandleBasic), "Basic candle subscription sample. Demonstrates how a candle subscription can created and consumed."},
        new object[] { "Subscriptions/Candle.Set", typeof(CandleSet), "Advanced candle subscription sample. Demonstrates how multiple candlestick subscriptions can be created and"
            + " consumed at the same time."},
        new object[] { "Subscriptions/OrderBook.Basic", typeof(OrderBookBasic), "Basic order book subscription sample. Demonstrates how an order book subscription can be created"
            + " and consumed."},
        new object[] { "Subscriptions/OrderBook.Set", typeof(OrderBookSet), "Advanced order book subscription sample. Demonstrates how multiple order book subscriptions can be"
            + " created and consumed at the same time."},
        new object[] { "Subscriptions/OrderBook.Arbitrage", typeof(OrderBookArbitrage), "Advanced order book subscription sample. Demonstrates how to monitor order books on two"
            + " different exchanges at the same time."},
        new object[] { "Subscriptions/Ticker.Basic", typeof(TickerBasic), "Basic ticker subscription sample. Demonstrates how a ticker subscription can be created and consumed."},
        new object[] { "Subscriptions/Ticker.Set", typeof(TickerSet), "Advanced ticker subscription sample. Demonstrates how multiple ticker subscriptions can be created and"
            + " consumed at the same time."},
        new object[] { "Trading/Order.Size.Small", typeof(SizeSmall), "Basic order sample. Demonstrates how small-sized orders can be placed. Requires credentials."},
        new object[] { "Trading/Order.Size.Large", typeof(SizeLarge), "Basic order sample. Demonstrates how larger-sized orders can be placed. Requires credentials and a valid"
            + " license."},
        new object[] { "Trading/Order.Updates", typeof(OrderUpdates), "Basic order's updates sample. Demonstrates how order's updates can be consumed. Requires credentials."},
        new object[] { "Trading/Order.Builder", typeof(RequestBuilder), "Basic order request builder sample. Demonstrates how orders can be build using the builder pattern."
            + " Requires credentials." },
        new object[] { "Trading/Order.Open.List", typeof(ListOpenOrders), "Sample that demonstrates how to get a list of open orders. Requires credentials." },
        new object[] { "Trading/TradeOrder.History", typeof(TradeOrderHistory), "Demonstrates getting historical trades and orders records. Requires credentials." },
        new object[] { "Trading/Budget", typeof(StrategyBudget), "Demonstrates working with trading strategy budget. Requires credentials." },
        new object[] { "Trading/BracketedOrder", typeof(BracketedOrder), "Sample that demonstrates placing bracketed order. Requires credentials." },
        new object[] { "Trading/Interactive", typeof(InteractiveTrading), "Sample that demonstrates creating orders and cancelling them in an interactive mode. Requires "
            + "credentials." },
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

            Console.WriteLine($$"""
                ERROR: {{error}}

                """);
        }

        Console.WriteLine($$"""
            Usage: {{nameof(WhalesSecret)}}.{{nameof(ScriptApiLib)}}.{{nameof(Samples)}} <sampleName> <exchangeMarket>

                sampleName - Name of the sample to run. Following values are supported:

            """);

        foreach (object[] sample in sampleDescriptions)
        {
            string name = (string)sample[0];
            string description = (string)sample[2];
            Console.WriteLine($"        {name} - {description}");
        }

        string markets = string.Join(',', Enum.GetValues<ExchangeMarket>());
        Console.WriteLine($$"""

                exchangeMarket - Exchange market to use in the sample. Supported values are {{markets}}.
            """);
    }
}