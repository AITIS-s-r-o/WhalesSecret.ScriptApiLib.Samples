using Skender.Stock.Indicators;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

Console.WriteLine("Initializing Binance exchange.");

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

Console.WriteLine("Connecting...");

// Connect to the exchange without API credentials to work with market data.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);
Console.WriteLine("Connection succeeded!");

await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(SymbolPair.BTC_USDT);

// Get historical candlestick data for the last three days for 1-minute candles.
DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Minute1, startTime, endTime);

// Compute RSI using Skender.Stock.Indicators.
// Add the package to the project using "dotnet add package Skender.Stock.Indicators".
// Add "using Skender.Stock.Indicators;" to the source file.
List<Quote> quotes = new();
foreach (Candle candle in candlestickData.Candles)
    quotes.Add(QuoteFromCandle(candle));

Candle lastClosedCandle = subscription.GetLatestClosedCandlestick(CandleWidth.Minute1);

Console.WriteLine();
Console.WriteLine($"Last closed candle: {lastClosedCandle}");

ReportRsi(quotes);

// Loop until the program is terminated using CTRL+C.
while (true)
{
    Console.WriteLine();
    Console.WriteLine("Waiting for the next closed candle...");

    try
    {
        lastClosedCandle = await subscription.WaitNextClosedCandlestickAsync(CandleWidth.Minute1);
    }
    catch (OperationCanceledException)
    {
        break;
    }

    Console.WriteLine($"New closed candle arrived: {lastClosedCandle}");
    quotes.Add(QuoteFromCandle(lastClosedCandle));
    ReportRsi(quotes);
}

static void ReportRsi(IEnumerable<Quote> quotes)
{
    IEnumerable<RsiResult> results = quotes.GetRsi();
    RsiResult lastRsi = results.Last();

    string interpretation = lastRsi.Rsi switch
    {
        < 30 => " (oversold!)",
        > 70 => " (overbought!)",
        _ => string.Empty
    };

    Console.WriteLine($"Current RSI: {lastRsi.Date} -> {lastRsi.Rsi}{interpretation}");
}

static Quote QuoteFromCandle(Candle candle)
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