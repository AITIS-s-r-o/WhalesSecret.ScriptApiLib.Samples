using Skender.Stock.Indicators;
using WhalesSecret.ScriptApiLib;
using WhalesSecret.TradeScriptLib.API.TradingV1;
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

CandlestickData candlestickData = await tradeClient.GetCandlesticksAsync(SymbolPair.BTC_USDT, CandleWidth.Hour1, startTime, endTime);

// Compute RSI using:
// <PackageReference Include="Skender.Stock.Indicators" Version="2.6.1" />
RsiResult? lastRsi = ComputeRsi(candlestickData.Candles);

if (lastRsi is not null)
{
    string action = lastRsi.Rsi switch
    {
        > 70 => "Sell",
        < 30 => "Buy",
        _ => "Hold"
    };

    Console.WriteLine($"Last RSI value is: {lastRsi?.Rsi}. The signal tells: {action}");
}
else
{
    Console.WriteLine($"No data to compute RSI.");
}

static RsiResult? ComputeRsi(IEnumerable<Candle> candles)
{
    IEnumerable<Quote> quotes = candles.Select(c => new Quote()
    {
        Date = c.Timestamp,
        Open = c.OpenPrice,
        High = c.HighPrice,
        Low = c.LowPrice,
        Close = c.ClosePrice,
        Volume = c.BaseVolume,
    });

    IEnumerable<RsiResult> results = quotes.GetRsi();

    return results.Last();
}