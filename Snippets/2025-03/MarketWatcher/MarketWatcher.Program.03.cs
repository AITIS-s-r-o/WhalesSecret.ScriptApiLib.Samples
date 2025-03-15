using Skender.Stock.Indicators;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
using WhalesSecret.TradeScriptLib.API.TradingV1.MarketData;
using WhalesSecret.TradeScriptLib.Entities;
using WhalesSecret.TradeScriptLib.Entities.MarketData;

await using ScriptApi scriptApi = await ScriptApi.CreateAsync();

// Initialize information about various exchange assets and coins.
await scriptApi.InitializeMarketAsync(ExchangeMarket.BinanceSpot);

// Connect to the exchange.
ConnectionOptions options = new(ConnectionType.MarketData);
await using ITradeApiClient tradeClient = await scriptApi.ConnectAsync(ExchangeMarket.BinanceSpot, options);

Console.WriteLine("🐋 got connected!");

DateTime endTime = DateTime.Now;
DateTime startTime = endTime.AddDays(-3);

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Minute1, startTime, endTime);

await using ICandlestickSubscription subscription = await tradeClient.CreateCandlestickSubscriptionAsync(SymbolPair.BTC_USDT);
Candle lastClosedCandle = subscription.GetLatestClosedCandlestick(CandleWidth.Minute1);

// Compute RSI using:
// <PackageReference Include="Skender.Stock.Indicators" Version="2.6.1" />
List<Quote> quotes = new();
foreach (Candle candle in candlestickData.Candles)
    quotes.Add(QuoteFromCandle(candle));

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