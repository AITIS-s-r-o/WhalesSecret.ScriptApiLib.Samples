using System;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Subscriptions;

/// <summary>
/// Helper methods for order book samples.
/// </summary>
public static class OrderBookHelper
{
    /// <summary>
    /// Prints the top bids and asks of the given order book to the console output.
    /// </summary>
    /// <param name="orderBook">Order book to print.</param>
    /// <param name="sideSize">Maximal number of bids and asks to print.</param>
    public static void PrintOrderBook(OrderBook orderBook, int sideSize = 5)
    {
        SymbolPair symbolPair = orderBook.SymbolPair;

        Console.WriteLine();
        for (int i = sideSize - 1; i >= 0; i--)
        {
            if (orderBook.Asks.Count < i)
                continue;

            Console.WriteLine($"  ask #{i + 1}: {orderBook.Asks[i].Quantity} {symbolPair.BaseSymbol} @ {orderBook.Asks[i].Price} {symbolPair.QuoteSymbol}");
        }

        Console.WriteLine("  ---------------------------------------");

        for (int i = 0; i < sideSize; i++)
        {
            if (orderBook.Bids.Count < i)
                break;

            Console.WriteLine($"  bid #{i + 1}: {orderBook.Bids[i].Quantity} {symbolPair.BaseSymbol} @ {orderBook.Bids[i].Price} {symbolPair.QuoteSymbol}");
        }

        Console.WriteLine();
    }
}