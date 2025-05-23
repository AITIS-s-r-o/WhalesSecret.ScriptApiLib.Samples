using System.Diagnostics.CodeAnalysis;
using System;
using Skender.Stock.Indicators;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using WhalesSecret.TradeScriptLib.Exceptions;

namespace WhalesSecret.ScriptApiLib.Samples.SharedLib;

/// <summary>
/// Helper functions for working with candles.
/// </summary>
public static class CandleUtils
{
    /// <summary>
    /// Converts Whale's Secret candle representation to OHLCV data format for <see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see>.
    /// </summary>
    /// <param name="candle">Whale's Secret candle to convert.</param>
    /// <returns><see href="https://dotnet.stockindicators.dev/">Skender.Stock.Indicators</see> quote representing the candle.</returns>
    public static Quote QuoteFromCandle(Candle candle)
    {
        Quote quote = new()
        {
            Date = candle.Timestamp,
            Open = candle.OpenPrice,
            High = candle.HighPrice,
            Low = candle.LowPrice,
            Close = candle.ClosePrice,
            Volume = candle.BaseVolume,
        };

        return quote;
    }

    /// <summary>
    /// Converts <see cref="CandleWidth"/> to a short string.
    /// </summary>
    /// <param name="candleWidth">Candle width to convert.</param>
    /// <returns>Short string representing the candle width.</returns>
    public static string CandleWidthToShortString(CandleWidth candleWidth)
    {
        return candleWidth switch
        {
            CandleWidth.Minute1 => "1m",
            CandleWidth.Minutes3 => "3m",
            CandleWidth.Minutes5 => "5m",
            CandleWidth.Minutes15 => "15m",
            CandleWidth.Minutes30 => "30m",
            CandleWidth.Hour1 => "1h",
            CandleWidth.Hours2 => "2h",
            CandleWidth.Hours4 => "4h",
            CandleWidth.Hours6 => "6h",
            CandleWidth.Hours8 => "8h",
            CandleWidth.Hours12 => "12h",
            CandleWidth.Day1 => "1d",
            CandleWidth.Days3 => "3d",
            CandleWidth.Week1 => "7w",
            CandleWidth.Month1 => "1M",
            _ => throw new SanityCheckException($"Unsupported candle width {candleWidth} provided."),
        };
    }
}