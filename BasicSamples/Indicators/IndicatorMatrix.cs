using System;
using System.Threading;
using System.Threading.Tasks;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;
using System.Collections.Generic;
using System.Linq;
using Skender.Stock.Indicators;
using WhalesSecret.TradeScriptLib.Exceptions;
using WhalesSecret.ScriptApiLib.Samples.SharedLib;
using System.Runtime.InteropServices;

namespace WhalesSecret.ScriptApiLib.Samples.BasicSamples.Indicators;

/// <summary>
/// Sample that demonstrates how to calculate many different indicators on different timeframes and a summary of all results. <see href="https://dotnet.stockindicators.dev/">
/// Skender.Stock.Indicators</see> is used for indicator calculation.
/// </summary>
public class IndicatorMatrix : IScriptApiSample
{
    /// <summary>Timeframes for which the sample calculates indicator values.</summary>
    private static readonly CandleWidth[] candleWidths =
    {
        CandleWidth.Minute1,
        CandleWidth.Minutes5,
        CandleWidth.Minutes15,
        CandleWidth.Minutes30,
        CandleWidth.Hour1,
        CandleWidth.Hours2,
        CandleWidth.Hours4,
        CandleWidth.Hours6,
        CandleWidth.Hours12,
        CandleWidth.Day1,
    };

    /// <summary>List of periods for moving average indicators that we calculate.</summary>
    private static readonly int[] maLookbacks = { 5, 10, 20, 50, 100, 200 };

    /// <summary>List of periods for RSI indicator that we calculate.</summary>
    private static readonly int[] rsiLookbacks = { 7, 9, 14, 21, 25 };

    /// <summary>List of periods for CCI indicator that we calculate.</summary>
    private static readonly int[] cciLookbacks = { 10, 14, 20, 30, 50 };

    /// <summary>List of periods for AO indicator that we calculate.</summary>
    private static readonly int[][] aoLookbacks =
    {
        new int[] { 5, 34 },
        new int[] { 3, 21 },
        new int[] { 8, 55 },
        new int[] { 10, 20 }
    };

    /// <summary>List of periods for ADX indicator that we calculate.</summary>
    private static readonly int[] adxLookbacks = { 7, 14, 21 };

    /// <summary>List of periods for BBP indicator that we calculate.</summary>
    private static readonly int[] bbpLookbacks = { 5, 10, 13, 20, 26 };

    /// <summary>Symbol pair used in the sample.</summary>
    private static readonly SymbolPair symbolPair = SymbolPair.BTC_USDT;

    /// <summary>List of quotes mapped to their candle widths.</summary>
    private readonly Dictionary<CandleWidth, List<Quote>> quotesByCandleWidth;

    /// <summary>Summary of interpretations of indicators mapped by composite keys created by <see cref="GetSummaryKey(CandleWidth, string)"/>.</summary>
    private readonly Dictionary<string, int> summary;

    /// <summary>Current price of <see cref="symbolPair"/> on the selected exchange market.</summary>
    private decimal currentPrice;

    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    public IndicatorMatrix()
    {
        this.quotesByCandleWidth = new();
        this.summary = new();
    }

    /// <inheritdoc/>
    public async Task RunSampleAsync(ExchangeMarket exchangeMarket)
    {
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(5));

        await using ScriptApi scriptApi = await ScriptApi.CreateAsync(timeoutCts.Token).ConfigureAwait(false);

        Console.WriteLine($"Connect to {exchangeMarket} exchange with a public connection.");
        ConnectionOptions connectionOptions = new(connectionType: ConnectionType.MarketData);
        await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(exchangeMarket, connectionOptions).ConfigureAwait(false);

        Console.WriteLine($"Public connection to {exchangeMarket} has been established successfully.");

        Console.WriteLine($"Getting current price for '{symbolPair}' on {exchangeMarket}.");
         
        IReadOnlyList<Ticker> tickers = await tradeClient.GetLatestTickersAsync(new SymbolPair[] { symbolPair }, timeoutCts.Token).ConfigureAwait(false);
        if (tickers.Count != 1)
            throw new SanityCheckException($"Expected a single ticker, got {tickers.Count}.");

        Ticker ticker = tickers[0];
        if (ticker.LastPrice is null)
            throw new SanityCheckException($"Unable to get current price for '{symbolPair}' on {exchangeMarket}.");

        this.currentPrice = ticker.LastPrice.Value;
        Console.WriteLine($"Current price of '{symbolPair}' on {exchangeMarket} is {this.currentPrice}.");

        Console.WriteLine("Get all quotes that are needed for indicator calculation.");

        foreach (CandleWidth candleWidth in candleWidths)
        {
            if (!candleWidth.ToTimeSpan(out TimeSpan? candleTimeSpan))
                throw new SanityCheckException($"Unable to convert candle width {candleWidth} to timespan.");

            TimeSpan historyNeeded = candleTimeSpan.Value * 210;
            DateTime now = DateTime.UtcNow;
            DateTime startTime = now.Add(-historyNeeded);
            CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(symbolPair, candleWidth, startTime: startTime, endTime: now, timeoutCts.Token)
                .ConfigureAwait(false);

            List<Quote> quotes = new(capacity: candlestickData.Candles.Count);
            foreach (Candle candle in candlestickData.Candles)
                quotes.Add(CandleUtils.QuoteFromCandle(candle));

            this.quotesByCandleWidth.Add(candleWidth, quotes);
        }

        Console.WriteLine();
        Console.WriteLine();

        this.MovingAverages();
        this.RelativeStrengthIndex();
        this.CommodityChannelIndex();
        this.AwesomeOscilator();
        this.AverageDirectionalIndex();
        this.BullBearPower();

        this.Summary();

        Console.WriteLine("Disposing trade API client and script API.");
    }

    /// <summary>
    /// Calculates and prints moving averages analysis.
    /// </summary>
    /// <seealso href="https://www.investopedia.com/terms/m/movingaverage.asp"/>
    private void MovingAverages()
    {
        Console.WriteLine("MOVING AVERAGES");
        Console.WriteLine("===============");
        Console.WriteLine();

        List<CandleWidth> buyCandleWidthsSma = new();
        List<CandleWidth> sellCandleWidthsSma = new();
        List<CandleWidth> neutralCandleWidthsSma = new();
        List<CandleWidth> buyCandleWidthsEma = new();
        List<CandleWidth> sellCandleWidthsEma = new();
        List<CandleWidth> neutralCandleWidthsEma = new();

        foreach (int lookback in maLookbacks)
        {
            buyCandleWidthsSma.Clear();
            sellCandleWidthsSma.Clear();
            neutralCandleWidthsSma.Clear();
            buyCandleWidthsEma.Clear();
            sellCandleWidthsEma.Clear();
            neutralCandleWidthsEma.Clear();

            foreach (CandleWidth candleWidth in candleWidths)
            {
                List<Quote> quotes = this.quotesByCandleWidth[candleWidth];
                IEnumerable<SmaResult> smaResult = quotes.GetSma(lookback);
                double? sma = smaResult.Last().Sma;
                if (sma is null)
                    throw new SanityCheckException($"Unable to calculate {lookback}-SMA.");

                IEnumerable<EmaResult> emaResult = quotes.GetEma(lookback);
                double? ema = emaResult.Last().Ema;
                if (ema is null)
                    throw new SanityCheckException($"Unable to calculate {lookback}-EMA.");

                decimal priceEmaDiff = Math.Abs(this.currentPrice - (decimal)ema.Value);
                decimal diffRatioEma = priceEmaDiff / this.currentPrice;

                decimal priceSmaDiff = Math.Abs(this.currentPrice - (decimal)sma.Value);
                decimal diffRatioSma = priceSmaDiff / this.currentPrice;

                // The current price is NOT within 3% of the SMA.
                if (diffRatioSma > 0.03m)
                {
                    if (this.currentPrice > (decimal)sma.Value)
                    {
                        buyCandleWidthsSma.Add(candleWidth);
                        this.SummaryBuy(candleWidth);
                    }
                    else
                    {
                        sellCandleWidthsSma.Add(candleWidth);
                        this.SummarySell(candleWidth);
                    }
                }
                else
                {
                    neutralCandleWidthsSma.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }

                // The current price is NOT within 3% of the EMA.
                if (diffRatioEma > 0.03m)
                {
                    if (this.currentPrice > (decimal)ema.Value)
                    {
                        buyCandleWidthsEma.Add(candleWidth);
                        this.SummaryBuy(candleWidth);
                    }
                    else
                    {
                        sellCandleWidthsEma.Add(candleWidth);
                        this.SummarySell(candleWidth);
                    }
                }
                else
                {
                    neutralCandleWidthsEma.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }
            }

            Console.WriteLine($"{lookback}-SMA buy:        {string.Join(", ", buyCandleWidthsSma.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"{lookback}-SMA sell:       {string.Join(", ", sellCandleWidthsSma.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"{lookback}-SMA neutral:    {string.Join(", ", neutralCandleWidthsSma.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"{lookback}-EMA buy:        {string.Join(", ", buyCandleWidthsEma.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"{lookback}-EMA sell:       {string.Join(", ", sellCandleWidthsEma.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"{lookback}-EMA neutral:    {string.Join(", ", neutralCandleWidthsEma.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Calculates and prints RSI analysis.
    /// </summary>
    /// <seealso href="https://www.investopedia.com/terms/r/rsi.asp"/>
    private void RelativeStrengthIndex()
    {
        Console.WriteLine("RSI");
        Console.WriteLine("===");
        Console.WriteLine();

        List<CandleWidth> oversoldCandleWidths = new();
        List<CandleWidth> overboughtCandleWidths = new();
        List<CandleWidth> neutralCandleWidths = new();

        foreach (int lookback in rsiLookbacks)
        {
            oversoldCandleWidths.Clear();
            overboughtCandleWidths.Clear();
            neutralCandleWidths.Clear();

            foreach (CandleWidth candleWidth in candleWidths)
            {
                List<Quote> quotes = this.quotesByCandleWidth[candleWidth];
                IEnumerable<RsiResult> rsiResult = quotes.GetRsi(lookback);
                double? rsi = rsiResult.Last().Rsi;
                if (rsi is null)
                    throw new SanityCheckException($"Unable to calculate RSI({lookback}).");

                if (rsi < 30)
                {
                    oversoldCandleWidths.Add(candleWidth);
                    this.SummaryBuy(candleWidth);
                }
                else if (rsi > 70)
                {
                    overboughtCandleWidths.Add(candleWidth);
                    this.SummarySell(candleWidth);
                }
                else
                {
                    neutralCandleWidths.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }
            }

            Console.WriteLine($"RSI({lookback}) oversold:      {string.Join(", ", oversoldCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"RSI({lookback}) overbought:    {string.Join(", ", overboughtCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"RSI({lookback}) neutral:       {string.Join(", ", neutralCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Calculates and prints CCI analysis.
    /// </summary>
    /// <seealso href="https://chartschool.stockcharts.com/table-of-contents/technical-indicators-and-overlays/technical-indicators/commodity-channel-index-cci"/>
    private void CommodityChannelIndex()
    {
        Console.WriteLine("CCI");
        Console.WriteLine("===");
        Console.WriteLine();

        List<CandleWidth> oversoldCandleWidths = new();
        List<CandleWidth> overboughtCandleWidths = new();
        List<CandleWidth> neutralCandleWidths = new();

        foreach (int lookback in cciLookbacks)
        {
            oversoldCandleWidths.Clear();
            overboughtCandleWidths.Clear();
            neutralCandleWidths.Clear();

            foreach (CandleWidth candleWidth in candleWidths)
            {
                List<Quote> quotes = this.quotesByCandleWidth[candleWidth];
                IEnumerable<CciResult> cciResult = quotes.GetCci(lookback);
                double? cci = cciResult.Last().Cci;
                if (cci is null)
                    throw new SanityCheckException($"Unable to calculate CCI({lookback}).");

                if (cci < -100)
                {
                    oversoldCandleWidths.Add(candleWidth);
                    this.SummaryBuy(candleWidth);
                }
                else if (cci > 100)
                {
                    overboughtCandleWidths.Add(candleWidth);
                    this.SummarySell(candleWidth);
                }
                else
                {
                    neutralCandleWidths.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }
            }

            Console.WriteLine($"CCI({lookback}) oversold:      {string.Join(", ", oversoldCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"CCI({lookback}) overbought:    {string.Join(", ", overboughtCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"CCI({lookback}) neutral:       {string.Join(", ", neutralCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }


    /// <summary>
    /// Calculates and prints AO analysis.
    /// </summary>
    /// <seealso href="https://dotnet.stockindicators.dev/indicators/Awesome/#content"/>
    private void AwesomeOscilator()
    {
        Console.WriteLine("AO");
        Console.WriteLine("==");
        Console.WriteLine();

        List<CandleWidth> buyCandleWidths = new();
        List<CandleWidth> sellCandleWidths = new();
        List<CandleWidth> neutralCandleWidths = new();

        foreach (int[] lookbacks in aoLookbacks)
        {
            buyCandleWidths.Clear();
            sellCandleWidths.Clear();
            neutralCandleWidths.Clear();

            foreach (CandleWidth candleWidth in candleWidths)
            {
                List<Quote> quotes = this.quotesByCandleWidth[candleWidth];
                IEnumerable<AwesomeResult> awesomeResult = quotes.GetAwesome(fastPeriods: lookbacks[0], slowPeriods: lookbacks[1]);
                double? ao = awesomeResult.Last().Oscillator;
                if (ao is null)
                    throw new SanityCheckException($"Unable to calculate AO({lookbacks[0]}, {lookbacks[1]}).");

                if (ao < -50)
                {
                    buyCandleWidths.Add(candleWidth);
                    this.SummaryBuy(candleWidth);
                }
                else if (ao > 50)
                {
                    sellCandleWidths.Add(candleWidth);
                    this.SummarySell(candleWidth);
                }
                else
                {
                    neutralCandleWidths.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }
            }

            Console.WriteLine($"AO({lookbacks[0]}, {lookbacks[1]}) buy:        {string.Join(", ", buyCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"AO({lookbacks[0]}, {lookbacks[1]}) sell:       {string.Join(", ", sellCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"AO({lookbacks[0]}, {lookbacks[1]}) neutral:    {string.Join(", ", neutralCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Calculates and prints ADX analysis.
    /// </summary>
    /// <seealso href="https://www.investopedia.com/articles/trading/07/adx-trend-indicator.asp"/>
    private void AverageDirectionalIndex()
    {
        Console.WriteLine("ADX");
        Console.WriteLine("===");
        Console.WriteLine();

        List<CandleWidth> buyCandleWidths = new();
        List<CandleWidth> sellCandleWidths = new();
        List<CandleWidth> neutralCandleWidths = new();

        foreach (int lookback in adxLookbacks)
        {
            buyCandleWidths.Clear();
            sellCandleWidths.Clear();
            neutralCandleWidths.Clear();

            foreach (CandleWidth candleWidth in candleWidths)
            {
                List<Quote> quotes = this.quotesByCandleWidth[candleWidth];
                IEnumerable<AdxResult> adxResult = quotes.GetAdx(lookback);
                double? adx = adxResult.Last().Adx;
                if (adx is null)
                    throw new SanityCheckException($"Unable to calculate ADX({lookback}).");

                if (adx > 25)
                {
                    double? mdi = adxResult.Last().Mdi;
                    if (mdi is null)
                        throw new SanityCheckException($"Unable to calculate -DI ADX({lookback}).");

                    double? pdi = adxResult.Last().Pdi;
                    if (pdi is null)
                        throw new SanityCheckException($"Unable to calculate +DI ADX({lookback}).");

                    if (mdi > pdi)
                    {
                        buyCandleWidths.Add(candleWidth);
                        this.SummaryBuy(candleWidth);
                    }
                    else
                    {
                        sellCandleWidths.Add(candleWidth);
                        this.SummarySell(candleWidth);
                    }
                }
                else
                {
                    neutralCandleWidths.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }
            }

            Console.WriteLine($"ADX({lookback}) buy:        {string.Join(", ", buyCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"ADX({lookback}) sell:       {string.Join(", ", sellCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"ADX({lookback}) neutral:    {string.Join(", ", neutralCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Calculates and prints BBP (also known as Elder-Ray Indicator) analysis.
    /// </summary>
    /// <seealso href="https://www.investopedia.com/articles/trading/03/022603.asp"/>
    private void BullBearPower()
    {
        Console.WriteLine("BBP");
        Console.WriteLine("===");
        Console.WriteLine();

        List<CandleWidth> buyCandleWidths = new();
        List<CandleWidth> sellCandleWidths = new();
        List<CandleWidth> neutralCandleWidths = new();

        foreach (int lookback in bbpLookbacks)
        {
            buyCandleWidths.Clear();
            sellCandleWidths.Clear();
            neutralCandleWidths.Clear();

            foreach (CandleWidth candleWidth in candleWidths)
            {
                List<Quote> quotes = this.quotesByCandleWidth[candleWidth];
                IEnumerable<ElderRayResult> bbpResult = quotes.GetElderRay(lookback);

                double? ema = bbpResult.Last().Ema;
                if (ema is null)
                    throw new SanityCheckException($"Unable to calculate EMA of BBP({lookback}).");

                double? bullLast = bbpResult.Last().BullPower;
                if (bullLast is null)
                    throw new SanityCheckException($"Unable to calculate last bull power of BBP({lookback}).");

                double? bearLast = bbpResult.Last().BearPower;
                if (bearLast is null)
                    throw new SanityCheckException($"Unable to calculate last bear power of BBP({lookback}).");

                double? bullSecondLast = bbpResult.TakeLast(2).First().BullPower;
                if (bullSecondLast is null)
                    throw new SanityCheckException($"Unable to calculate second to last bull power of BBP({lookback}).");

                double? bearSecondLast = bbpResult.TakeLast(2).First().BearPower;
                if (bearSecondLast is null)
                    throw new SanityCheckException($"Unable to calculate second to last bear power of BBP({lookback}).");

                decimal priceEmaDiff = Math.Abs(this.currentPrice - (decimal)ema.Value);
                decimal diffRatio = priceEmaDiff / this.currentPrice;

                bool bullRising = bullLast > bearLast;
                bool bearRising = bearLast > bullLast;
                bool bullFalling = !bullRising;
                bool bearFalling = !bearRising;
                bool isNeutral = true;

                // The current price is NOT within 3% of the EMA.
                if (diffRatio > 0.03m)
                {
                    if (this.currentPrice > (decimal)ema.Value)
                    {
                        if ((bullLast > 0) && bullRising && (bearLast < 0) && bearRising)
                        {
                            buyCandleWidths.Add(candleWidth);
                            this.SummaryBuy(candleWidth);
                            isNeutral = false;
                        }
                    }
                    else
                    {
                        if ((bullLast > 0) && bullFalling && (bearLast < 0) && bearFalling)
                        {
                            sellCandleWidths.Add(candleWidth);
                            this.SummarySell(candleWidth);
                            isNeutral = false;
                        }
                    }
                }

                if (isNeutral)
                {
                    neutralCandleWidths.Add(candleWidth);
                    this.SummaryNeutral(candleWidth);
                }
            }

            Console.WriteLine($"BBP({lookback}) buy:        {string.Join(", ", buyCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"BBP({lookback}) sell:       {string.Join(", ", sellCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine($"BBP({lookback}) neutral:    {string.Join(", ", neutralCandleWidths.Select(CandleUtils.CandleWidthToShortString))}");
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints summary of all the analyses.
    /// </summary>
    private void Summary()
    {
        Console.WriteLine("SUMMARY");
        Console.WriteLine("=======");
        Console.WriteLine();

        foreach (CandleWidth candleWidth in candleWidths)
        {
            string buyKey = this.GetSummaryBuyKey(candleWidth);
            _ = this.summary.TryGetValue(buyKey, out int buyCount);
            Console.WriteLine($"Buy {CandleUtils.CandleWidthToShortString(candleWidth)}:        {buyCount}");

            string sellKey = this.GetSummarySellKey(candleWidth);
            _ = this.summary.TryGetValue(sellKey, out int sellCount);
            Console.WriteLine($"Sell {CandleUtils.CandleWidthToShortString(candleWidth)}:       {sellCount}");

            string neutralKey = this.GetSummaryNeutralKey(candleWidth);
            _ = this.summary.TryGetValue(neutralKey, out int neutralCount);
            Console.WriteLine($"Neutral {CandleUtils.CandleWidthToShortString(candleWidth)}:    {neutralCount}");

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Gets a buy key for <see cref="summary"/> map.
    /// </summary>
    /// <inheritdoc cref="GetSummaryKey(CandleWidth, string)"/>
    private string GetSummaryBuyKey(CandleWidth candleWidth)
        => this.GetSummaryKey(candleWidth, "buy");

    /// <summary>
    /// Gets a sell key for <see cref="summary"/> map.
    /// </summary>
    /// <inheritdoc cref="GetSummaryKey(CandleWidth, string)"/>
    private string GetSummarySellKey(CandleWidth candleWidth)
        => this.GetSummaryKey(candleWidth, "sell");

    /// <summary>
    /// Gets a neutral key for <see cref="summary"/> map.
    /// </summary>
    /// <inheritdoc cref="GetSummaryKey(CandleWidth, string)"/>
    private string GetSummaryNeutralKey(CandleWidth candleWidth)
        => this.GetSummaryKey(candleWidth, "neutral");

    /// <summary>
    /// Gets a composite key for <see cref="summary"/> map.
    /// </summary>
    /// <param name="candleWidth">Candle width of the result.</param>
    /// <param name="result">Result string.</param>
    /// <returns>Composite key for <see cref="summary"/> map.</returns>
    private string GetSummaryKey(CandleWidth candleWidth, string result)
        => $"{candleWidth}-{result}";

    /// <summary>
    /// Adds a buy summary for the given candle width.
    /// </summary>
    /// <param name="candleWidth">Candle width of the result.</param>
    private void SummaryBuy(CandleWidth candleWidth)
        => this.AddSummary(candleWidth, "buy");

    /// <summary>
    /// Adds a sell summary for the given candle width.
    /// </summary>
    /// <param name="candleWidth">Candle width of the result.</param>
    private void SummarySell(CandleWidth candleWidth)
        => this.AddSummary(candleWidth, "sell");

    /// <summary>
    /// Adds a neutral summary for the given candle width.
    /// </summary>
    /// <param name="candleWidth">Candle width of the result.</param>
    private void SummaryNeutral(CandleWidth candleWidth)
        => this.AddSummary(candleWidth, "neutral");

    /// <summary>
    /// Adds a summary for the given candle width.
    /// </summary>
    /// <param name="candleWidth">Candle width of the result.</param>
    private void AddSummary(CandleWidth candleWidth, string result)
    {
        string key = this.GetSummaryKey(candleWidth, result);

        ref int value = ref CollectionsMarshal.GetValueRefOrAddDefault(this.summary, key, out _);
        value++;
    }
}