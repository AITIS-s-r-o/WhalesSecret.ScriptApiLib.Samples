using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.Subscriptions;

/// <summary>
/// Helper methods for order book samples.
/// </summary>
public static class OrderBookHelper
{
    /// <summary>
    /// Prints the top bids and asks of the given order book to the concole output.
    /// </summary>
    /// <param name="orderBook">Order book to print.</param>
    /// <param name="sideSize">Maximal number of bids and asks to print.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task PrintOrderBookAsync(OrderBook orderBook, int sideSize = 5)
    {
        SymbolPair symbolPair = orderBook.SymbolPair;

        await Console.Out.WriteLineAsync($"Up-to-date order book snapshot '{orderBook}' has been received.").ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        for (int i = sideSize - 1; i >= 0; i--)
        {
            if (orderBook.Asks.Count < i)
                continue;

            await Console.Out.WriteLineAsync($"  ask #{i + 1}: {orderBook.Asks[i].Quantity} {symbolPair.BaseSymbol} @ {orderBook.Asks[i].Price} {symbolPair.QuoteSymbol}")
                .ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("  ---------------------------------------").ConfigureAwait(false);

        for (int i = 0; i < sideSize; i++)
        {
            if (orderBook.Bids.Count < i)
                break;

            await Console.Out.WriteLineAsync($"  bid #{i + 1}: {orderBook.Bids[i].Quantity} {symbolPair.BaseSymbol} @ {orderBook.Bids[i].Price} {symbolPair.QuoteSymbol}")
                .ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
    }
}